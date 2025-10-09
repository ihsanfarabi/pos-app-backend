using MediatR;

namespace PosApp.Application.Abstractions.Idempotency;

public interface IIdempotentRequest<out TResponse> : IRequest<TResponse>
{
}
