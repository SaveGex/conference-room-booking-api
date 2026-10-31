using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Application.Common.Interfaces
{
    /// <summary>
    /// Database context contract exposed to the Application layer.
    /// </summary>
    public interface IBookingDbContext
    {
        DbSet<Room> Rooms { get; set; }
        DbSet<Booking> Bookings { get; set; }
        DbSet<Service> Services { get; set; }

        // Holds the room lock until the caller commits or disposes the transaction.
        Task<IDbContextTransaction> BeginRoomBookingAsync(Guid roomId, CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
