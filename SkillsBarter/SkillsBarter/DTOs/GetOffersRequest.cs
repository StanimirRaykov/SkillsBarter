namespace SkillsBarter.DTOs;

public class GetOffersRequest
{
    public string? Q { get; set; }
    public int? SkillId { get; set; }
    public string? Skill { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public void Validate()
    {
        if (Page < 1)
            Page = 1;

        if (PageSize < 1)
            PageSize = 10;

        if (PageSize > 100)
            PageSize = 100;

        Q = Q?.Trim();
        Skill = Skill?.Trim();
    }
}
