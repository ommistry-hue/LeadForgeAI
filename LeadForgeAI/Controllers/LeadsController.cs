using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LeadForgeAI.Data;
using LeadForgeAI.Models;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using System.Globalization;
using System.Text;

namespace LeadForgeAI.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LeadsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var jobs = await _context.Jobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View(jobs);
        }

        public async Task<IActionResult> ViewJob(int id)
        {
            var userId = _userManager.GetUserId(User);
            var job = await _context.Jobs
                .Include(j => j.Leads)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }

        public async Task<IActionResult> ExportCsv(int id)
        {
            var userId = _userManager.GetUserId(User);
            var job = await _context.Jobs
                .Include(j => j.Leads)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null)
            {
                return NotFound();
            }

            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            // Write headers
            csvWriter.WriteField("Domain");
            csvWriter.WriteField("Company Name");
            csvWriter.WriteField("Industry");
            csvWriter.WriteField("Employee Count");
            csvWriter.WriteField("Business Email");
            csvWriter.WriteField("Phone");
            csvWriter.WriteField("Lead Score");
            csvWriter.WriteField("Country");
            csvWriter.WriteField("Company Description");
            csvWriter.NextRecord();

            // Write data
            foreach (var lead in job.Leads)
            {
                csvWriter.WriteField(lead.Domain);
                csvWriter.WriteField(lead.CompanyName);
                csvWriter.WriteField(lead.Industry);
                csvWriter.WriteField(lead.EmployeeCount);
                csvWriter.WriteField(lead.BusinessEmail);
                csvWriter.WriteField(lead.Phone);
                csvWriter.WriteField(lead.LeadScore);
                csvWriter.WriteField(lead.Country);
                csvWriter.WriteField(lead.CompanyDescription);
                csvWriter.NextRecord();
            }

            streamWriter.Flush();
            var bytes = memoryStream.ToArray();

            return File(bytes, "text/csv", $"enriched_leads_{job.Id}_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> DeleteJob(int id)
        {
            var userId = _userManager.GetUserId(User);
            var job = await _context.Jobs
                .Include(j => j.Leads)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null)
            {
                return NotFound();
            }

            _context.Leads.RemoveRange(job.Leads);
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Job deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
