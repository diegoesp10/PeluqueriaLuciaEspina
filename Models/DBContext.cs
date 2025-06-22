using Microsoft.EntityFrameworkCore;


namespace PeluqueriaLuciaEspina.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Servicio> Servicios { get; set; }
        public DbSet<CortePelo> CortesPelo { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // CortePelo
            modelBuilder.Entity<CortePelo>()
                .Property(c => c.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<CortePelo>()
                .Property(c => c.PrecioBase)
                .HasColumnType("decimal(18,2)");

            // Servicio
            modelBuilder.Entity<Servicio>()
                .Property(s => s.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Servicio>()
                .Property(s => s.Precio)
                .HasColumnType("decimal(18,2)");

            base.OnModelCreating(modelBuilder);
        }
    }
}
