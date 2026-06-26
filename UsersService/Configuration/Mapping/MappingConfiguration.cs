using AutoMapper;
using UsersService.Models.Entities;
using UsersService.Models.Requests.Auth;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Users;


namespace UsersService.Configuration.Mapping
{
    public class MappingConfiguration : Profile
    {
        public MappingConfiguration()
        {
            CreateMap<ApplicationUser, UsersResponse>();
            CreateMap<RegistrationRequest, ApplicationUser>();
            CreateMap<UpdateUserRequest, ApplicationUser>();
            CreateMap<ApplicationUser, UserResponse>();
            CreateMap<Organization, UserOrganization>();
        }
    }
}
