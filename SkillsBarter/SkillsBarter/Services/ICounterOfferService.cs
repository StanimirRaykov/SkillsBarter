public interface ICounterOfferService
{
    Task<CounterOfferResponse?> CreateAsync(
        Guid proposalId,
        CreateCounterOfferRequest request,
        Guid userId);

    Task<bool> AcceptAsync(Guid counterOfferId, Guid userId);
    Task<bool> RejectAsync(Guid counterOfferId, Guid userId);
}
