using AutoMapper;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<CreateTaskDto, TaskItem>();
            CreateMap<CreateProgressDto, Progress>();

            CreateMap<TaskItem, TaskDto>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<Progress, ProgressDto>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<Unit, UnitDto>();

            CreateMap<User, UserDto>()
                .ForMember(dest => dest.UnitId,
                    opt => opt.MapFrom(src => src.UnitId))
                .ForMember(dest => dest.FullName,
                    opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.EmployeeCode,
                    opt => opt.MapFrom(src => src.EmployeeCode))
                .ForMember(dest => dest.IsApproved,
                    opt => opt.MapFrom(src => src.IsApproved))
                .ForMember(dest => dest.PhoneNumber,
                    opt => opt.MapFrom(src => src.PhoneNumber));

            CreateMap<ReportReview, ReviewDto>()
                .ForMember(dest => dest.ProgressId, opt => opt.MapFrom(src => src.ProgressId))
                .ForMember(dest => dest.Approve, opt => opt.MapFrom(src => src.IsApproved))
                .ForMember(dest => dest.Comment, opt => opt.MapFrom(src => src.Comment));

            CreateMap<UploadFile, UploadFileDto>();
            CreateMap<TaskComment, CommentDto>();
            CreateMap<SubTask, SubTaskDto>();
            CreateMap<Project, ProjectDto>();
        }
    }
}
