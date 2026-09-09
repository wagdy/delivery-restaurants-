using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public CustomerService(ApplicationDbContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<PagedResult<CustomerAnalyticsResponse>> GetAnalyticsPagedAsync(int page, int pageSize, string? search)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = BuildAnalyticsQuery(search);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<CustomerAnalyticsResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Stream> ExportAnalyticsAsync(string? search)
    {
        var customers = await BuildAnalyticsQuery(search).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customer Insights");

        // Same header-styling recipe as BulkOrderImportService.GenerateTemplate() - bold
        // white text on the app's primary blue - so every generated workbook in this app
        // looks consistent.
        string[] headers =
        {
            "Full Name", "Contact Info", "Total Points", "Current Points", "Membership Tier",
            "Total Orders", "Average Check", "Total Lifetime Value", "Last Order Date",
            "Punch Card Enrolled", "Punch Card Redeems"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(63, 81, 181);
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var i = 0; i < customers.Count; i++)
        {
            var c = customers[i];
            var row = i + 2;

            sheet.Cell(row, 1).Value = c.FullName;
            sheet.Cell(row, 2).Value = c.ContactInfo;
            sheet.Cell(row, 3).Value = c.TotalPoints;
            sheet.Cell(row, 4).Value = c.CurrentPoints;
            sheet.Cell(row, 5).Value = c.MembershipTier;
            sheet.Cell(row, 6).Value = c.TotalOrders;
            sheet.Cell(row, 7).Value = c.AverageCheck;
            sheet.Cell(row, 8).Value = c.TotalLifetimeValue;
            sheet.Cell(row, 9).Value = c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToString("yyyy-MM-dd") : "Never";
            sheet.Cell(row, 10).Value = c.IsPunchCardEnrolled ? "Yes" : "No";
            sheet.Cell(row, 11).Value = c.PunchCardRedeemsCount;
        }

        sheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Shared by the paged screen and the export so the two can never disagree about what
    // a customer's numbers are. Ordered by order count, matching both predecessor
    // screens' own default ordering (busiest customers first).
    //
    // IsPunchCardEnrolled/PunchCardRedeemsCount below are correlated subqueries against
    // LoyaltyCampaignProgress/LoyaltyPunchTransactions (there's no direct nav property
    // from AppUser to either, by design), same left-join-friendly shape as the
    // LoyaltyProfiles join - EF Core still translates the whole thing into one SQL
    // statement per page of results, not N+1 queries.
    private IQueryable<CustomerAnalyticsResponse> BuildAnalyticsQuery(string? search)
    {
        var query =
            from u in _context.Users
            where u.Role == UserRole.Customer && !u.IsDeleted
            join p in _context.LoyaltyProfiles on u.Id equals p.AppUserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            select new CustomerAnalyticsResponse
            {
                Id = u.Id,
                FullName = u.FullName,
                ContactInfo = !string.IsNullOrEmpty(u.PhoneNumber) ? u.PhoneNumber! : (u.Email ?? "—"),
                PhoneNumber = u.PhoneNumber,
                TotalPoints = profile != null ? profile.TotalLifetimePoints : 0,
                CurrentPoints = profile != null ? profile.CurrentPoints : 0,
                MembershipTier = profile != null ? (profile.MembershipTier ?? "Unranked") : "Unranked",
                TotalOrders = u.Orders.Count(),
                AverageCheck = u.Orders.Any() ? u.Orders.Average(o => o.TotalAmount) : 0m,
                TotalLifetimeValue = u.Orders.Sum(o => o.TotalAmount),
                LastOrderDate = u.Orders.Any() ? u.Orders.Max(o => o.CreatedAt) : (DateTime?)null,
                IsPunchCardEnrolled = _context.LoyaltyCampaignProgress.Any(cp => cp.CustomerId == u.Id),
                PunchCardRedeemsCount = _context.LoyaltyPunchTransactions.Count(
                    t => t.TransactionType == PunchTransactionType.RewardClaimed && t.Progress.CustomerId == u.Id)
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.FullName.Contains(term) || c.ContactInfo.Contains(term));
        }

        return query.OrderByDescending(c => c.TotalOrders);
    }

    public async Task<ServiceResult<CustomerAnalyticsResponse>> UpdateCustomerAsync(string id, UpdateCustomerRequest request)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer && !u.IsDeleted);
        if (user is null)
        {
            return ServiceResult<CustomerAnalyticsResponse>.Failure("Customer not found.");
        }

        var phone = request.PhoneNumber.Trim();
        if (!string.Equals(user.PhoneNumber, phone, StringComparison.Ordinal) &&
            await _userManager.Users.AnyAsync(u => u.Id != id && u.PhoneNumber == phone))
        {
            return ServiceResult<CustomerAnalyticsResponse>.Failure("An account with this phone number already exists.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = phone;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ServiceResult<CustomerAnalyticsResponse>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());
        }

        var updated = await BuildAnalyticsQuery(null).FirstAsync(c => c.Id == id);
        return ServiceResult<CustomerAnalyticsResponse>.Success(updated);
    }

    public async Task<ServiceResult<bool>> DeleteCustomerAsync(string id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer && !u.IsDeleted);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("Customer not found.");
        }

        // Soft delete, not Remove() - see AppUser.IsDeleted for why (Orders/OrderReviews/
        // LoyaltyProfile FKs, plus this needing to also block login and hide the account
        // from the Scanner and the "Registered Customer" order search - not just this list).
        user.IsDeleted = true;
        await _userManager.UpdateAsync(user);

        return ServiceResult<bool>.Success(true);
    }
}
