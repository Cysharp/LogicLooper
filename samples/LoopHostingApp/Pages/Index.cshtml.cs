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
            _logger = logger;
            _looperPool = looperPool;
        }

        public void OnGet()
        {

        }

        public IActionResult OnPost()
        {
            new LifeGameLoop(_looperPool, _logger);

            return RedirectToPage("Index");
        }
    }
}
