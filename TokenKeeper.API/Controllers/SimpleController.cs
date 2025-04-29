using Microsoft.AspNetCore.Mvc;
using TokenRepository.Core;

namespace TokenRepository.API.Controllers;
[ApiController]
[Route("[controller]")]
public class SimpleController(ILogger<SimpleController> logger) : ControllerBase
{
    private readonly ILogger<SimpleController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var keeper = new TokenStateKeeper(new StateKeeperFactory());

        for (var i = 1; i <= 10; i++)
        {
            keeper.Seed($"{i}", "test");
        }

        keeper.Stage("1", "11", "test1");
        keeper.Commit();

        keeper.Stage("11", null, null);
        keeper.Commit();

        keeper.Stage("11", null, null);
        keeper.Commit();

        keeper.Stage("12", null, null);
        keeper.Commit();

        keeper.Stage("12", null, null);
        keeper.Commit();

        var diff = keeper.GetCommittedDiff();
        var uDiff = keeper.GetUncommittedDiff();
        //keeper.Commit();

        var full = keeper.GetFullCurrentSnapshot();
        keeper.TryGetSnapshot($"9", out var snapshot);
        return Ok(full);
    }
}
