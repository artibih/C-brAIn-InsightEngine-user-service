using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UsersService.Models.Entities;

namespace UsersService.DbContext.Configurations
{
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organizations");

            builder.HasKey(o => o.Id);

            builder.Property(o => o.Name)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(o => o.Description)
                   .HasMaxLength(1000);

            builder.Property(o => o.Address)
                   .HasMaxLength(300);

            builder.Property(o => o.City)
                   .HasMaxLength(100);

            builder.Property(o => o.Country)
                   .HasMaxLength(100);

            builder.Property(o => o.Phone)
                   .HasMaxLength(20);

            builder.Property(o => o.Email)
                   .HasMaxLength(100);

            builder.Property(o => o.Website)
                   .HasMaxLength(200);

            builder.Property(o => o.Logo)
                   .HasMaxLength(500);

            builder.Property(o => o.CreatedDate)
                   .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")
                   .ValueGeneratedOnAdd();

            builder.Property(o => o.ModifiedDate)
                   .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

            builder.Property(o => o.NotificationEmails)
               .HasColumnType("text[]")
               .IsRequired(false);

            builder.HasIndex(o => o.Email)
                .HasDatabaseName("IX_Organizations_Email");

        }
    }
}
