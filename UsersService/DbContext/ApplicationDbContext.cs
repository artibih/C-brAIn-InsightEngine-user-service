using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UsersService.DbContext.Configurations;
using UsersService.Models.Entities;

namespace UsersService.DbContext
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public override int SaveChanges()
        {
            SetOrganizationModifiedDate();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetOrganizationModifiedDate();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetOrganizationModifiedDate();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetOrganizationModifiedDate();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void SetOrganizationModifiedDate()
        {
            foreach (var entry in ChangeTracker.Entries<Organization>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedDate = DateTime.UtcNow;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(b =>
            {
                b.Property(u => u.Id).IsRequired();
                b.Property(u => u.UserName).IsRequired();
                b.Property(u => u.NormalizedUserName).IsRequired();
                b.Property(u => u.Email).IsRequired();
                b.Property(u => u.NormalizedEmail).IsRequired();
                b.Property(u => u.PasswordHash).IsRequired();
                b.Property(u => u.EmailConfirmed).IsRequired();
                b.Property(u => u.FirstName).IsRequired();
                b.Property(u => u.LastName).IsRequired();
                b.Property(u => u.SecurityStamp).IsRequired();
                b.Property(u => u.LockoutEnabled).IsRequired().HasDefaultValue(false);
                b.Property(u => u.LockoutEnd).IsRequired(false);
                b.Property(u => u.AccessFailedCount).IsRequired();
                b.Property(u => u.ConcurrencyStamp).IsRequired();
                b.Property(u => u.IsUsingTemporaryPassword).HasDefaultValue(true);
                b.Property(u => u.TemporaryPasswordCreatedAt).IsRequired(false);
                b.Ignore(u => u.PhoneNumber);
                b.Ignore(u => u.PhoneNumberConfirmed);
                b.Ignore(u => u.TwoFactorEnabled);
                b.HasOne(u => u.Organization)
                 .WithMany()
                 .HasForeignKey(u => u.OrganizationId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ApplicationRole>(b =>
            {
                b.ToTable("Roles");
            });
            builder.Entity<IdentityUserRole<int>>(b =>
            {
                b.ToTable("UserRoles");
            });
            builder.Entity<IdentityUserToken<int>>(b =>
            {
                b.ToTable("UserTokens");
            });

            builder.Entity<RefreshToken>()
               .HasIndex(r => r.TokenHash)
               .IsUnique();

            builder.Entity<RefreshToken>()
                .HasIndex(r => r.UserId)
                .IsUnique();

            builder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.ApplyConfiguration(new RoleConfiguration());
            builder.ApplyConfiguration(new UserConfiguration());
            builder.ApplyConfiguration(new UserRoleConfiguration());
            builder.ApplyConfiguration(new OrganizationConfiguration());

        }
    }

}
