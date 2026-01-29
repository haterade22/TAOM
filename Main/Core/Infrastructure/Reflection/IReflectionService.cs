namespace TAOM.Core.Infrastructure;

public interface IReflectionService
{
    TReturn GetFieldValue<TOwner, TReturn>(TOwner owner, string name) where TOwner : class;

    void SetFieldValue<TOwner>(TOwner owner, string name, object value) where TOwner : class;

    TReturn GetPropertyValue<TOwner, TReturn>(TOwner owner, string name) where TOwner : class;

    object GetPropertyValue<TOwner>(TOwner owner, string name) where TOwner : class;

    void SetPropertyValue<TOwner>(TOwner owner, string name, object value) where TOwner : class;

    object CallPrivateMethod<TType>(TType owner, string name, object[] values) where TType : class;
}
