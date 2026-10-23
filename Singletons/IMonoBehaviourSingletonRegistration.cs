namespace Vecerdi.Extensions.DependencyInjection;

internal interface IMonoBehaviourSingletonRegistration {
    Type Type { get; }
    bool InstanceExists { get; }
    MissingSingletonInstanceBehavior MissingInstanceBehavior { get; }
    SingletonPersistence Persistence { get; }
}
