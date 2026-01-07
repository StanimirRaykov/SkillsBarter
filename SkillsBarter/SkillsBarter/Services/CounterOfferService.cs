public class CounterOfferService : ICounterOfferService
{
    private readonly ApplicationDbContext _context;
    private readonly IAgreementService _agreementService;

    public CounterOfferService(
        ApplicationDbContext context,
        IAgreementService agreementService)
    {
        _context = context;
        _agreementService = agreementService;
    }

    public async Task<CounterOfferResponse?> CreateAsync(
        Guid proposalId,
        CreateCounterOfferRequest request,
        Guid userId)
    {
        var proposal = await _context.Proposals.FindAsync(proposalId);
        if (proposal == null) return null;

        if (proposal.PendingResponseFromUserId != userId)
            return null;

        var counter = new CounterOffer
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            CreatedByUserId = userId,
            Terms = request.Terms,
            Message = request.Message
        };

        proposal.PendingResponseFromUserId =
            proposal.ProposerId == userId
                ? proposal.OfferOwnerId
                : proposal.ProposerId;

        _context.CounterOffers.Add(counter);
        await _context.SaveChangesAsync();

        return new CounterOfferResponse
        {
            Id = counter.Id,
            ProposalId = proposalId,
            Terms = counter.Terms,
            Message = counter.Message,
            CreatedAt = counter.CreatedAt,
            Status = counter.Status,
            CreatedByName = "User" // mapper / include ако искаш
        };
    }

    public async Task<bool> AcceptAsync(Guid counterOfferId, Guid userId)
    {
        var counter = await _context.CounterOffers
            .Include(c => c.Proposal)
            .FirstOrDefaultAsync(c => c.Id == counterOfferId);

        if (counter == null) return false;

        if (counter.CreatedByUserId == userId)
            return false;

        counter.Status = CounterOfferStatus.Accepted;

        var proposal = counter.Proposal;
        proposal.Status = ProposalStatus.Accepted;
        proposal.PendingResponseFromUserId = null;

        await _agreementService.CreateAgreementAsync(
            proposal.OfferId,
            proposal.ProposerId,
            proposal.OfferOwnerId,
            counter.Terms,
            null);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(Guid counterOfferId, Guid userId)
    {
        var counter = await _context.CounterOffers.FindAsync(counterOfferId);
        if (counter == null) return false;

        if (counter.CreatedByUserId == userId)
            return false;

        counter.Status = CounterOfferStatus.Rejected;
        await _context.SaveChangesAsync();
        return true;
    }
}
