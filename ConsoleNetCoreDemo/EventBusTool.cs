using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleNetCoreDemo
{
    class EventBusTool
    {
        private IServiceCollection services;

        public EventBusTool()
        {
            services = new ServiceCollection();
            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
            services.AddSingleton<AddUserEventHandler, AddUserEventHandler>();

            services.AddSingleton<IEventBus, InMemoryEventBus>(sp =>
            {
                //var logger = sp.GetRequiredService<ILogger<InMemoryEventBus>>();
                var logger = new LoggerFactory().CreateLogger<InMemoryEventBus>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                return new InMemoryEventBus(services.BuildServiceProvider(), logger, eventBusSubcriptionsManager);
            });
        }

        public void Build()
        {
            var provider = services.BuildServiceProvider();

            var eventBus = provider.GetRequiredService<IEventBus>();
            eventBus.Subscribe<AddUserEvent, AddUserEventHandler>();

            eventBus.Publish(new AddUserEvent() { });
        }

        public interface IEventBus
        {
            void Publish(IntegrationEvent @event);
            void Subscribe<T, TH>()
                    where T : IntegrationEvent
                    where TH : IIntegrationEventHandler<T>;
            void Unsubscribe<T, TH>()
                   where TH : IIntegrationEventHandler<T>
                   where T : IntegrationEvent;
        }
        public interface IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {

        }

        public class IntegrationEvent
        {
            public IntegrationEvent()
            {
                Id = Guid.NewGuid();
                CreationDate = DateTime.UtcNow;
            }
            public Guid Id { get; }
            public DateTime CreationDate { get; }
        }

        public class SubscriptionInfo
        {
            public bool IsDynamic { get; }
            public Type HandlerType { get; }
            private SubscriptionInfo(bool isDynamic, Type handlerType)
            {
                IsDynamic = isDynamic;
                HandlerType = handlerType;
            }
            public static SubscriptionInfo Dynamic(Type handlerType)
            {
                return new SubscriptionInfo(true, handlerType);
            }
            public static SubscriptionInfo Typed(Type handlerType)
            {
                return new SubscriptionInfo(false, handlerType);
            }
        }
        public interface IEventBusSubscriptionsManager
        {
            event EventHandler<string> OnEventRemoved;
            IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>()
                where T : IntegrationEvent;
            IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName);
            void AddSubscription<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>;
            void RemoveSubscription<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>;
            bool HasSubscriptionsForEvent(string eventName);
            string GetEventKey<T>();
        }
        public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
        {
            private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
            private readonly List<Type> _eventTypes;
            public event EventHandler<string> OnEventRemoved;
            public InMemoryEventBusSubscriptionsManager()
            {
                _handlers = new Dictionary<string, List<SubscriptionInfo>>();
                _eventTypes = new List<Type>();
            }
            public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
            {
                var key = GetEventKey<T>();
                return GetHandlersForEvent(key);
            }
            public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];
            public void AddSubscription<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>
            {
                var eventName = GetEventKey<T>();
                DoAddSubscription(typeof(TH), eventName, isDynamic: false);
                _eventTypes.Add(typeof(T));
            }
            private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
            {
                if (!HasSubscriptionsForEvent(eventName))
                {
                    _handlers.Add(eventName, new List<SubscriptionInfo>());
                }

                if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
                {
                    throw new ArgumentException(
                        $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
                }

                if (isDynamic)
                {
                    _handlers[eventName].Add(SubscriptionInfo.Dynamic(handlerType));
                }
                else
                {
                    _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
                }
            }
            public void RemoveSubscription<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>
            {
                var handlerToRemove = FindSubscriptionToRemove<T, TH>();
                var eventName = GetEventKey<T>();
                DoRemoveHandler(eventName, handlerToRemove);
            }
            private SubscriptionInfo FindSubscriptionToRemove<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>
            {
                var eventName = GetEventKey<T>();
                return DoFindSubscriptionToRemove(eventName, typeof(TH));
            }
            private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
            {
                if (subsToRemove != null)
                {
                    _handlers[eventName].Remove(subsToRemove);
                    if (!_handlers[eventName].Any())
                    {
                        _handlers.Remove(eventName);
                        var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                        if (eventType != null)
                        {
                            _eventTypes.Remove(eventType);
                        }
                        RaiseOnEventRemoved(eventName);
                    }
                }
            }
            private SubscriptionInfo DoFindSubscriptionToRemove(string eventName, Type handlerType)
            {
                if (!HasSubscriptionsForEvent(eventName))
                {
                    return null;
                }
                return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);
            }
            private void RaiseOnEventRemoved(string eventName)
            {
                var handler = OnEventRemoved;
                if (handler != null)
                {
                    OnEventRemoved(this, eventName);
                }
            }

            public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

            public string GetEventKey<T>()
            {
                return typeof(T).Name;
            }
        }

        public class InMemoryEventBus : IEventBus
        {
            private readonly IServiceProvider _provider;
            private readonly ILogger<InMemoryEventBus> _logger;
            private readonly IEventBusSubscriptionsManager _manager;
            public InMemoryEventBus(
                IServiceProvider provider,
                ILogger<InMemoryEventBus> logger,
                IEventBusSubscriptionsManager manager)
            {
                _provider = provider;
                _logger = logger;
                _manager = manager;
            }

            public void Publish(IntegrationEvent e)
            {
                var eventType = e.GetType();
                var handlers = _manager.GetHandlersForEvent(eventType.Name);

                foreach (var handlerInfo in handlers)
                {
                    var handler = _provider.GetService(handlerInfo.HandlerType);

                    var method = handlerInfo.HandlerType.GetMethod("Handle");

                    method.Invoke(handler, new object[] { e });
                }
            }

            public void Subscribe<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>
            {
                _manager.AddSubscription<T, TH>();
            }

            public void Unsubscribe<T, TH>()
                where T : IntegrationEvent
                where TH : IIntegrationEventHandler<T>
            {
                _manager.RemoveSubscription<T, TH>();
            }
        }

        public class AddUserEvent : IntegrationEvent
        {
            public string UserName => "行走的炸药包";
        }
        public class AddUserEventHandler : IIntegrationEventHandler<AddUserEvent>
        {
            public void Handle(AddUserEvent dto)
            {
                Console.WriteLine($"Id：{ dto.Id },时间：{dto.CreationDate.ToString()}");
            }
        }

    }
}
