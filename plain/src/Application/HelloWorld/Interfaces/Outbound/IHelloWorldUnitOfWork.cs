using Application.Shared.Interfaces;

namespace Application.HelloWorld.Interfaces.Outbound;

/// <summary>
/// Unit of work for HelloWorld feature atomicity boundary.
/// All HelloWorld repositories sharing this UnitOfWork are atomic together.
/// </summary>
public interface IHelloWorldUnitOfWork : IUnitOfWork;
