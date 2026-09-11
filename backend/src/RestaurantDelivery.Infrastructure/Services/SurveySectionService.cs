using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class SurveySectionService : ISurveySectionService
{
    private readonly ApplicationDbContext _context;

    public SurveySectionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SurveyMatrixSectionResponse>> GetAllAsync()
    {
        var sections = await _context.SurveyMatrixSections.OrderBy(s => s.Name).ToListAsync();
        return sections.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<SurveyMatrixSectionResponse>> CreateAsync(SurveyMatrixSectionRequest request)
    {
        var name = request.Name.Trim();

        // Case-insensitive duplicate check - same reasoning as CategoryService's own
        // GetByNameAsync guard: without it, "Service" and "service" would both exist as
        // distinct sections, which is exactly the near-duplicate problem this whole
        // feature exists to prevent.
        if (await _context.SurveyMatrixSections.AnyAsync(s => s.Name.ToLower() == name.ToLower()))
        {
            return ServiceResult<SurveyMatrixSectionResponse>.Failure("A section with this name already exists.");
        }

        var section = new SurveyMatrixSection { Name = name };
        _context.SurveyMatrixSections.Add(section);
        await _context.SaveChangesAsync();

        return ServiceResult<SurveyMatrixSectionResponse>.Success(MapResponse(section));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var section = await _context.SurveyMatrixSections.FindAsync(id);
        if (section is null)
        {
            return ServiceResult<bool>.Failure("Section not found.");
        }

        // Same "check first, friendly failure" convention as CategoryService.DeleteAsync
        // and RoleService.DeleteAsync - SurveyQuestion.MatrixSectionId is
        // DeleteBehavior.Restrict, so without this check a delete would instead surface
        // as a raw DbUpdateException (Postgres FK violation).
        var questionCount = await _context.SurveyQuestions.CountAsync(q => q.MatrixSectionId == id && !q.IsDeleted);
        if (questionCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this section because {questionCount} question(s) still use it. Reassign or delete them first.");
        }

        _context.SurveyMatrixSections.Remove(section);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static SurveyMatrixSectionResponse MapResponse(SurveyMatrixSection section) => new()
    {
        Id = section.Id,
        Name = section.Name
    };
}
