using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Africa.Server.Data;
using System.ComponentModel.DataAnnotations;

namespace Africa.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicationsController : ControllerBase
    {
        private readonly ApplicationsContext _context;

        public ApplicationsController(ApplicationsContext context)
        {
            _context = context;
        }

        // POST: api/Applications
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ApplicationCreateResult> PostApplication(CreateApplicationModel applicationModel)
        {
            var application = new Application();
            application.Name = applicationModel.Name;
            application.PhoneNumber = applicationModel.PhoneNumber;
            application.Email = applicationModel.Email;
            

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            var result = new ApplicationCreateResult();
            result.Id = application.Id;
            result.Name = application.Name;

            return result;
        }
    }
}

public class CreateApplicationModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid Phone Number")]
    [StringLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class ApplicationCreateResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
