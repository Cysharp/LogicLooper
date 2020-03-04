using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.LogicLooper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace LoopHostingApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ILogicLooperPool _looperPool;

        public int RunningActions => _looperPool.Loopers.Sum(x => x.ApproximatelyRunningActions);
        public IReadOnlyList<World> RunningWorlds => LifeGameLoop.All.Select(x => x.World).ToArray();

        public IndexModel(ILogger<IndexModel> logger, ILogicLooperPool looperPool)
        {
            // The parameter is provided via Dependency Injection.
            //   - See also: Startup.ConfigureServices method.
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _looperPool = looperPool ?? throw new ArgumentNullException(nameof(looperPool));
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // Example: Create a new world of life-game and register it into the loop.
            LifeGameLoop.CreateNew(_looperPool, _logger);

            return RedirectToPage("Index");
        }
    }
}
