﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GitHub.DistributedTask.WebApi;
using GitHub.Services.Location;
using GitHub.Services.WebApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Runner.Server.Models;

namespace Runner.Server.Controllers
{
    [ApiController]
    // [Authorize]
    [Route("{owner}/{repo}/_apis/v1/[controller]")]
    public class AgentController : VssControllerBase
    {

        private readonly ILogger<AgentController> _logger;
        private IMemoryCache _cache;

        private SqLiteDb _context;

        public AgentController(ILogger<AgentController> logger, IMemoryCache cache, SqLiteDb context)
        {
            _logger = logger;
            _cache = cache;
            _context = context;
        }

        private static int lastId = 0;

        [HttpPost("{poolId}")]
        public async Task<IActionResult> Post(int poolId) {
            TaskAgent agent = await FromBody<TaskAgent>();
            agent.Authorization.AuthorizationUrl = new Uri($"{Request.Scheme}://{Request.Host.Host ?? (HttpContext.Connection.RemoteIpAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? ("[" + HttpContext.Connection.RemoteIpAddress.ToString() + "]") : HttpContext.Connection.RemoteIpAddress.ToString())}:{Request.Host.Port ?? HttpContext.Connection.RemotePort}/test/auth/v1/");
            agent.Authorization.ClientId = Guid.NewGuid();
            Agent _agent = Agent.CreateAgent(_cache, _context, poolId, agent);
            try {
                await _context.SaveChangesAsync(HttpContext.RequestAborted);
            } catch {
                _agent.Id = ++lastId;
                await _context.SaveChangesAsync(HttpContext.RequestAborted);
            }
            lastId = _agent.Id;
            // _agent.AddToCache(_cache);
            return await Ok(agent);
        }

        [HttpGet("{poolId}/{agentId}")]
        public TaskAgent Get(int poolId, int agentId)
        {
            return Agent.GetAgent(_cache, _context, poolId, agentId).TaskAgent;
        }

        [HttpGet("{poolId}")]
        public VssJsonCollectionWrapper<List<TaskAgent>> Get(int poolId, [FromQuery] string agentName)
        {
            return new VssJsonCollectionWrapper<List<TaskAgent>> (
                (from agent in Pool.GetPoolById(_cache, _context, poolId)?.Agents ?? new List<Agent>() where agent != null && agent.TaskAgent.Name == agentName select agent.TaskAgent).ToList()
            );
        }
    }
}
