using InboundEndpoint.Controllers;
using InboundEndpoint.DTO;
using InboundEndpoint.Repository;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Eventing.Reader;

namespace InboundEndpoint.Services
{
    public class UserService(ILogger<LogController> logger, UserEntity userEntity, LogController logController)
    {
    }
}
