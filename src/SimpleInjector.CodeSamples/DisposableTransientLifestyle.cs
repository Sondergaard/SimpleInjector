﻿namespace SimpleInjector.CodeSamples
{
    using System;
    using System.Linq.Expressions;
    using SimpleInjector.Advanced;

    /// <summary>
    /// Allows registering instances with a <see cref="Lifestyle.Transient">Transient</see> lifestyle, while
    /// allowing them to get disposed on the boundary of a supplied <see cref="ScopedLifestyle"/>.
    /// </summary>
    public class DisposableTransientLifestyle : Lifestyle
    {
        private static readonly object ItemKey = new object();
        private readonly ScopedLifestyle scopedLifestyle;

        public DisposableTransientLifestyle(ScopedLifestyle scopedLifestyle)
            : base("Transient (Disposes on " + scopedLifestyle.Name + " boundary)")
        {
            this.scopedLifestyle = scopedLifestyle;
        }

        private interface IDisposableRegistration
        {
            ScopedLifestyle ScopedLifestyle { get; }
        }

        public override int Length => Transient.Length;

        public static void EnableForContainer(Container container)
        {
            bool alreadyInitialized = container.ContainerScope.GetItem(ItemKey) != null;

            if (!alreadyInitialized)
            {
                AddGlobalDisposableInitializer(container);

                container.ContainerScope.SetItem(ItemKey, ItemKey);
            }
        }

        protected override Registration CreateRegistrationCore(Type concreteType, Container c) =>
            new DisposableRegistration(this.scopedLifestyle, this, c, concreteType);

        protected override Registration CreateRegistrationCore<TService>(Func<TService> ic, Container c) =>
            new DisposableRegistration(this.scopedLifestyle, this, c, typeof(TService), ic);

        private static void TryEnableTransientDisposalOrThrow(Container container)
        {
            bool alreadyInitialized = container.ContainerScope.GetItem(ItemKey) != null;

            if (!alreadyInitialized)
            {
                if (container.IsLocked)
                {
                    throw new InvalidOperationException(
                        "Please make sure DisposableTransientLifestyle.EnableForContainer(Container) is " +
                        "called during initialization.");
                }

                EnableForContainer(container);
            }
        }

        private static void AddGlobalDisposableInitializer(Container container) =>
            container.RegisterInitializer(RegisterForDisposal, ShouldApplyInitializer);

        private static bool ShouldApplyInitializer(InitializerContext context) =>
            context.Registration is IDisposableRegistration;

        private static void RegisterForDisposal(InstanceInitializationData data)
        {
            if (data.Instance is IDisposable instance)
            {
                var registation = (IDisposableRegistration)data.Context.Registration;
                registation.ScopedLifestyle.RegisterForDisposal(data.Context.Registration.Container, instance);
            }
        }

        private sealed class DisposableRegistration : Registration, IDisposableRegistration
        {
            internal DisposableRegistration(
                ScopedLifestyle s, Lifestyle l, Container c, Type concreteType, Func<object> ic = null)
                : base(l, c, concreteType, ic)
            {
                this.ScopedLifestyle = s;
                DisposableTransientLifestyle.TryEnableTransientDisposalOrThrow(c);
            }

            public ScopedLifestyle ScopedLifestyle { get; }

            public override Expression BuildExpression() => this.BuildTransientExpression();
        }
    }
}