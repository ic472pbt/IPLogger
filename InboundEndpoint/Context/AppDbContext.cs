namespace InboundEndpoint.Context
{
    using InboundEndpoint.Model;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
    using System.Numerics;

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<UserData>()
                .HasMany(u => u.ConnectionsV4)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .IsRequired();

            modelBuilder.Entity<UserData>()
                .HasMany(u => u.ConnectionsV6)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .IsRequired();
        }

        public DbSet<UserData> Users { get; set; }
        public DbSet<ConnectionDataV4> ConnectionsV4 { get; set; }
        public DbSet<ConnectionDataV6> ConnectionsV6 { get; set; }
    }
}
