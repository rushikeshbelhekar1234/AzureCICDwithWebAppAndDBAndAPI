using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class TodoCreateRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string? Title { get; set; }

        public bool IsCompleted { get; set; }
    }
}
