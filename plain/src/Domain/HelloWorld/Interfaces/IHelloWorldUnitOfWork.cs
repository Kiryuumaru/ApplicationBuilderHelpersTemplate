using Domain.Shared.Interfaces;

namespace Domain.HelloWorld.Interfaces;

/// <summary>
/// Unit of work for HelloWorld feature atomicity boundary.
/// All HelloWorld repositories sharing this UnitOfWork are atomic together.
/// </summary>
public interface IHelloWorldUnitOfWork : IUnitOfWork;
