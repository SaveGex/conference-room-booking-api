using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Reflection;

namespace Infrastructure.Persistence
{
    internal class BookingDbContext : DbContext, IBookingDbContext
    {
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
        {
        }

        public async Task<IDbContextTransaction> BeginRoomBookingAsync(
            Guid roomId, CancellationToken cancellationToken = default)
        {
            var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                // Lock the existing room, even when it has no bookings yet.
                // Every booking writer must acquire this lock before checking availability.
                await Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT Id FROM Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {roomId}",
                    cancellationToken);
                return transaction;
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
