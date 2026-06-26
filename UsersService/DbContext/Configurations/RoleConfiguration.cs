using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UsersService.Models.Entities;

namespace UsersService.DbContext.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            builder.HasData(
                new ApplicationRole { Id = 1, Name = "SuperAdmin", NormalizedName = "SUPERADMIN", ConcurrencyStamp = "5eaaaed2-a0f4-49bb-b70e-c4d499eed800" },
                new ApplicationRole { Id = 2, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "01e87ccd-1948-467c-8b1c-9bb10225f786" },
                new ApplicationRole { Id = 3, Name = "OrganizationAdmin", NormalizedName = "ORGANIZATIONADMIN", ConcurrencyStamp = "646098b2-a8f3-4b4e-9a7f-f4937a095aa6" },
                new ApplicationRole { Id = 5, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "c09b9590-6dec-4ec7-ab32-1b4c3110008c" },
                new ApplicationRole { Id = 7, Name = "CBrainUser", NormalizedName = "CBRAINUSER", ConcurrencyStamp = "a1d3f8e2-7b6c-4e5a-9f01-2c8d4e6b0a73" }
            );
        }
    }
}
