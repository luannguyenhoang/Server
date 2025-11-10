using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Models;

namespace HoodLab.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<Size> Sizes { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<ProductVariantSize> ProductVariantSizes { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Slider> Sliders { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.ColorId }).IsUnique();
        });

        modelBuilder.Entity<ProductVariantSize>(entity =>
        {
            entity.HasIndex(e => new { e.ProductVariantId, e.SizeId }).IsUnique();
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ProductVariantId, e.SizeId }).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.Token).IsUnique();
        });
    }
}


