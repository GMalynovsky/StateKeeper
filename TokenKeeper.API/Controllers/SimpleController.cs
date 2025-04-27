using Microsoft.AspNetCore.Mvc;
using TokenKeeper.Core;

namespace TokenKeeper.API.Controllers;
[ApiController]
[Route("[controller]")]
public class SimpleController(ILogger<SimpleController> logger) : ControllerBase
{
    private readonly ILogger<SimpleController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var keeper = new TokenStateKeeper(new StateKeeperFactory());

        for (var i = 0; i < 10; i++)
        {
            keeper.Seed($"{i}", "test");
        }

        for (var i = 0; i < 5; i++)
        {
            keeper.Stage($"{i * 2}", $"222{i * 2}", "test changed");
        }

        for (var i = 10; i < 15; i++)
        {
            keeper.Stage(null, $"{i}", "test inserted");
        }

        keeper.Commit();

        for (var i = 10; i < 15; i++)
        {
            keeper.Stage($"{i}", $"333{i}", "test inserted changed");
        }

        keeper.Commit();

        for (var i = 10; i < 15; i++)
        {
            keeper.Stage($"333{i}", $"444{i}", "test inserted changed second time");
        }

        keeper.Commit();

        for (var i = 0; i < 5; i++)
        {
            keeper.Stage($"{i}", null, null);
        }

        keeper.Commit();

        var diff = keeper.GetCommittedDiff();
        var uDiff = keeper.GetUncommittedDiff();
        //keeper.Commit();

        var full = keeper.GetFullCurrentSnapshot();
        keeper.TryGetSnapshot($"9", out var snapshot);
        return Ok(full);
    }
}
