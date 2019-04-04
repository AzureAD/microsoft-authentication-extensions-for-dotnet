using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.ResourceManager;
using Newtonsoft.Json;

namespace WebAppTestWithAzureSDK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly IResourceManagementClient _client;

        public ValuesController(IResourceManagementClient client)
        {
            _client = client;
        }

        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            var firstPage = _client.ResourceGroups.List();
            return firstPage.Select(i => i.Name).ToList();
        }

        // GET api/values/5
        [HttpGet("{name}")]
        public ActionResult<string> Get(string name)
        {
            var sub = _client.ResourceGroups.Get(name);
            return JsonConvert.SerializeObject(sub);
        }
    }
}
