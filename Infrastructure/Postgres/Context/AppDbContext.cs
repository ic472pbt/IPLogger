namespace Infrastructure.Postgres.Context
{
    using Infrastructure.Postgres.Model;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;
        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                _ = optionsBuilder.UseNpgsql(_connectionString);
            }
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
