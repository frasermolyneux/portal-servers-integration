using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Models;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Controllers.V1;

[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/info")]
public class ApiInfoController : ControllerBase
{
    [HttpGet]
    public IActionResult GetInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";

        return Ok(new ApiInfoDto
        {
            Version = informationalVersion,
            BuildVersion = informationalVersion.Split('+')[0],
            AssemblyVersion = assemblyVersion
        });
    }
}
