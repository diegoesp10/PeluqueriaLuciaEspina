using Microsoft.EntityFrameworkCore;

namespace PeluqueriaLuciaEspina.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<ServicioGenerico> ServiciosGenericos { get; set; }
        public DbSet<ServicioCliente> ServiciosClientes { get; set; }
        public DbSet<ServicioGenericoRealizado> ServiciosGenericosRealizados { get; set; }
        public DbSet<Cliente> Clientes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Servicio Generico
            modelBuilder.Entity<ServicioGenerico>()
                .Property(s => s.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<ServicioGenerico>()
                .Property(s => s.Precio)
                .HasColumnType("decimal(18,2)");

            // Cliente
            modelBuilder.Entity<Cliente>()
                .Property(c => c.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Cliente>()
                .Property(c => c.Apellido)
                .IsRequired()
                .HasMaxLength(100);

            // ServicioCliente
            modelBuilder.Entity<ServicioCliente>()
                .HasOne(sc => sc.Cliente)
                .WithMany()
                .HasForeignKey(sc => sc.ClienteId);

            // ServicioGenericoRealizado
            modelBuilder.Entity<ServicioGenericoRealizado>()
                .HasOne(sgr => sgr.ServicioGenerico)
                .WithMany(sg => sg.Realizados)
                .HasForeignKey(sgr => sgr.ServicioGenericoId);

            modelBuilder.Entity<ServicioGenericoRealizado>()
                .HasOne(sgr => sgr.ServicioCliente)
                .WithMany(sc => sc.ServiciosRealizados)
                .HasForeignKey(sgr => sgr.ServicioClienteId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
