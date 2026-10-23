namespace Vecerdi.Extensions.DependencyInjection;

internal interface IMonoBehaviourSingletonRegistration {
    Type Type { get; }
    MissingSingletonInstanceBehavior MissingInstanceBehavior { get; }
    SingletonPersistence Persistence { get; }
}
