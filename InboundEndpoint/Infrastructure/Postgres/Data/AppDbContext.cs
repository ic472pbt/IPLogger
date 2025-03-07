using InboundEndpoint.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace InboundEndpoint.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);           
            Database.EnsureCreated();
        }
        public DbSet<SessionEntity> Sessions { get; set; }        
    }
}
