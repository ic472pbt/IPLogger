namespace InboundEndpoint.Infrastructure.Postgres.Data
{
    using InboundEndpoint.Infrastructure.Postgres.Entity;
    using Microsoft.EntityFrameworkCore;
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UserEntity>()
                .HasMany(u => u.Connections)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId);
            
            modelBuilder.Entity<ConnectionEntity>().HasIndex(c => c.IpAddress);

            modelBuilder.Entity<ConnectionEntity>()
                .HasOne(c => c.IPEntity)
                .WithOne(i => i.Connection)
                .HasForeignKey<ConnectionEntity>(c => c.IpAddress);

            Database.EnsureCreated();
        }
        public DbSet<SessionEntity> Sessions { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<ConnectionEntity> Connections { get; set; }
        public DbSet<IPEntity> IPEntities { get; set; }
    }
}
