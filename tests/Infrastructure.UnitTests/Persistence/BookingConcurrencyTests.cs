using System.Data.Common;
using Application.Bookings.Commands.CreateBooking;
using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Infrastructure.UnitTests.Persistence;

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_SQLSERVER")))
            Skip = "Set BOOKING_TEST_SQLSERVER to run against SQL Server (requires create database permission).";
    }
}

public class BookingConcurrencyTests
{
    [SqlServerTheory]
    [Trait("Category", "Integration")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParallelBookings_RejectOnlyOverlappingSlots(bool overlapping)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BOOKING_TEST_SQLSERVER"))
        {
            InitialCatalog = "BookingConcurrency_" + Guid.NewGuid().ToString("N")
        };
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlServer(connection.ConnectionString).Options;
        await using var setup = new BookingDbContext(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var pause = new PauseAfterAvailabilityCheck();
        var databaseCreated = false;
        try
        {
            await setup.Database.EnsureCreatedAsync(ct);
            databaseCreated = true;
            var room = Room.Create("Concurrency test", 10, new Money(100, "USD"));
            setup.Rooms.Add(room);
            await setup.SaveChangesAsync(ct);

            await using var first = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
                .UseSqlServer(connection.ConnectionString).AddInterceptors(pause).Options);
            var secondStarted = new SignalCommandStarted();
            await using var second = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
                .UseSqlServer(connection.ConnectionString).AddInterceptors(secondStarted).Options);
            var pricing = new Mock<IRentalPricingService>();
            pricing.Setup(p => p.CalculateRoomCost(It.IsAny<Money>(), It.IsAny<TimeSlot>()))
                .Returns(new Money(200, "USD"));
            var start = new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var firstCommand = new CreateBookingCommand(room.Id, new TimeSlot(start, start.AddHours(2)), []);
            var secondStart = start.AddHours(overlapping ? 1 : 2);
            var secondCommand = new CreateBookingCommand(room.Id, new TimeSlot(secondStart, secondStart.AddHours(2)), []);

            var firstTask = new CreateBookingCommandHandler(first, pricing.Object).Handle(firstCommand, ct);
            await pause.Checked.Task.WaitAsync(ct);
            var secondTask = new CreateBookingCommandHandler(second, pricing.Object).Handle(secondCommand, ct);
            await secondStarted.Started.Task.WaitAsync(ct);

            // Keep the first request between its availability query and insert while
            // the second connection attempts to book the same room.
            var finishedWhileFirstPaused = await Task.WhenAny(secondTask, Task.Delay(300, ct)) == secondTask;
            pause.Continue.TrySetResult();
            await firstTask;
            if (overlapping)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await secondTask);
                Assert.Contains("already booked", error.Message);
            }
            else
            {
                await secondTask;
            }

            Assert.False(finishedWhileFirstPaused, "The second booking must wait for the first transaction.");
            Assert.Equal(overlapping ? 1 : 2, await setup.Bookings.CountAsync(ct));
        }
        finally
        {
            pause.Continue.TrySetResult();
            // Only this test's uniquely named database is removed.
            if (databaseCreated)
                await setup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class PauseAfterAvailabilityCheck : DbCommandInterceptor
    {
        public TaskCompletionSource Checked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [Bookings]", StringComparison.Ordinal))
            {
                Checked.TrySetResult();
                await Continue.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class SignalCommandStarted : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
