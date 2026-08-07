using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity.Tests;

// Hand-authored equivalents of the source-generator output. They exercise the runtime contract
// without adding an analyzer reference to the isolated redesign solution.
internal sealed class ReactivePerson : IReactiveObject
{
    private readonly Dependency _nameDependency = new();
    private readonly Dependency _ageDependency = new();
    private string _name = string.Empty;
    private int _age;

    internal string Name
    {
        get
        {
            _nameDependency.Track();
            return _name;
        }
        set
        {
            if (EqualityComparer<string>.Default.Equals(_name, value))
            {
                return;
            }
            _name = value;
            _nameDependency.Trigger();
        }
    }

    internal int Age
    {
        get
        {
            _ageDependency.Track();
            return _age;
        }
        set
        {
            if (_age == value)
            {
                return;
            }
            _age = value;
            _ageDependency.Trigger();
        }
    }

    internal ReactivePersonReferences ToReferences() => new(this);

    internal RawValues ToRawValues() => new(this);

    object IReactiveObject.ToRaw() => this;

    Dependency? IReactiveObject.GetDependency(string propertyName) => propertyName switch
    {
        nameof(Name) => _nameDependency,
        nameof(Age) => _ageDependency,
        _ => null,
    };

    void IReactiveTraversable.Traverse(ReactiveTraversal traversal)
    {
        traversal.Visit(Name);
        traversal.Visit(Age);
    }

    internal sealed class RawValues
    {
        private readonly ReactivePerson _owner;

        internal RawValues(ReactivePerson owner) => _owner = owner;

        internal string Name
        {
            get => _owner._name;
            set => _owner._name = value;
        }

        internal int Age
        {
            get => _owner._age;
            set => _owner._age = value;
        }
    }
}

internal sealed class ReactivePersonReferences
{
    internal ReactivePersonReferences(ReactivePerson owner)
    {
        Name = Reactive.ToRef(() => owner.Name, value => owner.Name = value);
        Age = Reactive.ToRef(() => owner.Age, value => owner.Age = value);
    }

    internal ReactiveValue<string> Name { get; }

    internal ReactiveValue<int> Age { get; }
}

internal sealed class ReactiveOrder : IReactiveObject
{
    private readonly Dependency _customerDependency = new();
    private readonly Dependency _totalDependency = new();
    private ReactivePerson _customer = new();
    private int _total;

    internal ReactivePerson Customer
    {
        get
        {
            _customerDependency.Track();
            return _customer;
        }
        set
        {
            if (ReferenceEquals(_customer, value))
            {
                return;
            }
            _customer = value;
            _customerDependency.Trigger();
        }
    }

    internal int Total
    {
        get
        {
            _totalDependency.Track();
            return _total;
        }
        set
        {
            if (_total == value)
            {
                return;
            }
            _total = value;
            _totalDependency.Trigger();
        }
    }

    object IReactiveObject.ToRaw() => this;

    Dependency? IReactiveObject.GetDependency(string propertyName) => propertyName switch
    {
        nameof(Customer) => _customerDependency,
        nameof(Total) => _totalDependency,
        _ => null,
    };

    void IReactiveTraversable.Traverse(ReactiveTraversal traversal)
    {
        traversal.Visit(Customer);
        traversal.Visit(Total);
    }
}

internal sealed class ShallowBox : IReactiveObject
{
    private readonly Dependency _contentDependency = new();
    private readonly Dependency _versionDependency = new();
    private ReactivePerson _content = new();
    private int _version;

    internal ReactivePerson Content
    {
        get
        {
            _contentDependency.Track();
            return _content;
        }
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }
            _content = value;
            _contentDependency.Trigger();
        }
    }

    internal int Version
    {
        get
        {
            _versionDependency.Track();
            return _version;
        }
        set
        {
            if (_version == value)
            {
                return;
            }
            _version = value;
            _versionDependency.Trigger();
        }
    }

    object IReactiveObject.ToRaw() => this;

    Dependency? IReactiveObject.GetDependency(string propertyName) => propertyName switch
    {
        nameof(Content) => _contentDependency,
        nameof(Version) => _versionDependency,
        _ => null,
    };

    void IReactiveTraversable.Traverse(ReactiveTraversal traversal)
    {
        _ = Content;
        _ = Version;
    }
}

internal sealed class ReadonlyProfile : IReactiveObject
{
    private readonly Dependency _handleDependency = new();
    private string _handle = string.Empty;

    internal string Handle
    {
        get
        {
            _handleDependency.Track();
            return _handle;
        }
        set
        {
        }
    }

    bool IReactiveObject.IsReadOnly => true;

    bool IReactiveReadOnly.IsReadOnly => true;

    object IReactiveObject.ToRaw() => this;

    Dependency? IReactiveObject.GetDependency(string propertyName) =>
        propertyName == nameof(Handle) ? _handleDependency : null;

    void IReactiveTraversable.Traverse(ReactiveTraversal traversal) => traversal.Visit(Handle);
}
