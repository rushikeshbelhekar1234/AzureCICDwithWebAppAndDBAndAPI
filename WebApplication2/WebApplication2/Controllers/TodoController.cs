using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WebApplication2.Models;
using WebApplication2.Repositories;

namespace WebApplication2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TodoController : ControllerBase
    {
        private readonly TodoRepository _repo;
        private readonly IMemoryCache _cache;

        public TodoController(TodoRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var cacheKey = $"todos:page:{page}:size:{pageSize}";
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<TodoDto>? dto))
            {
                var items = await _repo.GetPageAsync(page, pageSize);
                dto = items.Select(i => new TodoDto { Id = i.Id, Title = i.Title, IsCompleted = i.IsCompleted });
                _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(30));
            }

            return Ok(dto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _repo.GetByIdAsync(id);
            if (item == null) return NotFound();
            var dto = new TodoDto { Id = item.Id, Title = item.Title, IsCompleted = item.IsCompleted };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TodoCreateRequest req)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var item = new Models.TodoItem { Title = req.Title, IsCompleted = req.IsCompleted };
            var id = await _repo.CreateAsync(item);
            var created = await _repo.GetByIdAsync(id);
            var dto = new TodoDto { Id = created!.Id, Title = created.Title, IsCompleted = created.IsCompleted };
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TodoUpdateRequest req)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            if (id != req.Id) return BadRequest("Id mismatch");
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return NotFound();
            existing.Title = req.Title;
            existing.IsCompleted = req.IsCompleted;
            var ok = await _repo.UpdateAsync(existing);
            if (!ok) return StatusCode(500);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return NotFound();
            var ok = await _repo.DeleteAsync(id);
            if (!ok) return StatusCode(500);
            return NoContent();
        }
    }
}
