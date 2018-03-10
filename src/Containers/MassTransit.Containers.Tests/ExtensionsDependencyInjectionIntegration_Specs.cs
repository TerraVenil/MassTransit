﻿// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Containers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ExtensionsDependencyInjectionIntegration;
    using GreenPipes;
    using Microsoft.Extensions.DependencyInjection;
    using Saga;
    using Scenarios;


    public class ExtensionsDependencyInjectionIntegration_Consumer :
        When_registering_a_consumer
    {
        readonly IServiceProvider _services;

        public ExtensionsDependencyInjectionIntegration_Consumer()
        {
            var collection = new ServiceCollection();

            collection.AddScoped<SimpleConsumer>();

            collection.AddMassTransit(x =>
            {
                x.AddConsumer<SimpleConsumer>();
            });

            collection.AddScoped<ISimpleConsumerDependency, SimpleConsumerDependency>();
            collection.AddScoped<AnotherMessageConsumer, AnotherMessageConsumerImpl>();

            _services = collection.BuildServiceProvider();
        }

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            configurator.LoadFrom(_services);
        }
    }


    public class ExtensionsDependencyInjectionIntegration_Saga :
        When_registering_a_saga
    {
        readonly IServiceProvider _services;

        public ExtensionsDependencyInjectionIntegration_Saga()
        {
            var collection = new ServiceCollection();

            collection.AddMassTransit(x =>
            {
                x.AddSaga<SimpleSaga>();
            });

            collection.AddSingleton<ISagaRepository<SimpleSaga>,  InMemorySagaRepository<SimpleSaga>>();

            _services = collection.BuildServiceProvider();
        }

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            configurator.LoadFrom(_services);
        }

        protected override ISagaRepository<T> GetSagaRepository<T>()
        {
            return _services.GetService<ISagaRepository<T>>();
        }
    }


    public interface IIdentifier
    {
        Guid Id { get; set; }
    }


    public class Identifier : IIdentifier
    {
        public Guid Id { get; set; }
    }


    public class CorrelationIdentityFilter<T> : IFilter<T> where T : class, ConsumeContext
    {
        public void Probe(ProbeContext context)
        {
        }

        public async Task Send(T context, IPipe<T> next)
        {
            if (context.TryGetPayload(out IServiceScope scope))
            {
                var identifier = scope.ServiceProvider.GetRequiredService<IIdentifier>();
                identifier.Id = Guid.NewGuid();
            }

            await next.Send(context);
        }
    }

    public class CorrelationIdentitySpecification<T> : IPipeSpecification<T> where T : class, ConsumeContext
    {
        public void Apply(IPipeBuilder<T> builder)
        {
            builder.AddFilter(new CorrelationIdentityFilter<T>());
        }

        public IEnumerable<ValidationResult> Validate()
        {
            return Enumerable.Empty<ValidationResult>();
        }
    }


    public class ExtensionsDependencyInjectionIntegration_ScopedConsumer :
        When_registering_a_consumer_with_scope_filter
    {
        readonly IServiceProvider _services;

        public ExtensionsDependencyInjectionIntegration_ScopedConsumer()
        {
            var collection = new ServiceCollection();

            collection.AddScoped<ConsumerForScopedFilter>();

            collection.AddScoped<IIdentifier, Identifier>();

            collection.AddMassTransit(x =>
            {
                x.AddConsumer<ConsumerForScopedFilter>();
            });

            _services = collection.BuildServiceProvider();
        }

        protected override void ConfigureInMemoryBus(IInMemoryBusFactoryConfigurator configurator)
        {
            configurator.UseScope(_services);
            configurator.AddPipeSpecification(new CorrelationIdentitySpecification<ConsumeContext>());
        }

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            configurator.LoadFrom(_services);
        }
    }
}