using Contracts.Domain;
using Infrastructure.Postgres;
using Microsoft.AspNetCore.Mvc;

namespace QueryMe.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class QueryController(ILogger<QueryController> logger, Postgres postgres) : ControllerBase
    {

        private readonly ILogger<QueryController> _logger = logger;

        [HttpGet("LastUserConnectionInfo")]
        public async Task<IActionResult> LastUserConnectionInfo(long UserId)
        {
            var userConnectionInfo = await postgres.GetUserConnectionInfo(UserId);
            if (userConnectionInfo == null)
            {
                return NotFound();
            }
            return Ok(userConnectionInfo);
        }

        [HttpGet("UsersByIpPrefix")]
        public async Task<IActionResult> UsersByIpPrefix(string IpPrefix)
        {
            var users = await postgres.FindUsersByIpPrefix(IpPrefix);
            if (users.Count() == 0)
            {
                return NotFound();
            }
            return Ok(users);
        }

        [HttpGet("IpsByUserId")]
        public async Task<IActionResult> IpsByUserId(long UserId)
        {
            var ips = (await Task.WhenAll(postgres.GetAllV4IpsForUser(UserId), postgres.GetAllV6IpsForUser(UserId)))
                .SelectMany(ipList => ipList)
                .ToList();
            if (ips.Count == 0)
            {
                return NotFound();
            }
            return Ok(ips);
        }
    }
}
