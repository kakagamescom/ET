using System;
using System.Collections.Generic;

namespace ET
{
    [ObjectSystem]
    public class MsgIdTypeComponentAwakeSystem: AwakeSystem<MsgIdTypeComponent>
    {
        public override void Awake(MsgIdTypeComponent self)
        {
            MsgIdTypeComponent.Instance = self;
            self.Awake();
        }
    }

    [ObjectSystem]
    public class MsgIdTypeComponentDestroySystem: DestroySystem<MsgIdTypeComponent>
    {
        public override void Destroy(MsgIdTypeComponent self)
        {
            MsgIdTypeComponent.Instance = null;
        }
    }

    public class MsgIdTypeComponent: Entity
    {
        public static MsgIdTypeComponent Instance;

        private HashSet<ushort> _outerActorMessage = new HashSet<ushort>();

        private readonly Dictionary<ushort, Type> _msgIdTypes = new Dictionary<ushort, Type>();
        private readonly Dictionary<Type, ushort> _typeMsgIds = new Dictionary<Type, ushort>();

        private readonly Dictionary<Type, Type> _requestResponse = new Dictionary<Type, Type>();

        public void Awake()
        {
            _msgIdTypes.Clear();
            _typeMsgIds.Clear();
            _requestResponse.Clear();

            HashSet<Type> types = Game.EventSystem.GetTypes(typeof(MessageAttribute));
            foreach (Type type in types)
            {
                object[] attrs = type.GetCustomAttributes(typeof(MessageAttribute), false);
                if (attrs.Length == 0)
                {
                    continue;
                }

                MessageAttribute messageAttribute = attrs[0] as MessageAttribute;
                if (messageAttribute == null)
                {
                    continue;
                }

                _msgIdTypes.Add(messageAttribute.MsgId, type);
                _typeMsgIds.Add(type, messageAttribute.MsgId);

                if (MsgIdHelper.IsOuterMessage(messageAttribute.MsgId) && typeof(IActorMessage).IsAssignableFrom(type))
                {
                    _outerActorMessage.Add(messageAttribute.MsgId);
                }

                // 检查request response
                if (typeof(IRequest).IsAssignableFrom(type))
                {
                    if (typeof(IActorLocationMessage).IsAssignableFrom(type))
                    {
                        _requestResponse.Add(type, typeof(ActorResponse));
                        continue;
                    }

                    attrs = type.GetCustomAttributes(typeof(ResponseTypeAttribute), false);
                    if (attrs.Length == 0)
                    {
                        Log.Error($"not found responseType: {type}");
                        continue;
                    }

                    ResponseTypeAttribute responseTypeAttribute = attrs[0] as ResponseTypeAttribute;
                    _requestResponse.Add(type, responseTypeAttribute.Type);
                }
            }
        }

        public bool IsOutrActorMessage(ushort opcode)
        {
            return _outerActorMessage.Contains(opcode);
        }

        public ushort GetOpcode(Type type)
        {
            return _typeMsgIds[type];
        }

        public Type GetType(ushort opcode)
        {
            return _msgIdTypes[opcode];
        }

        public Type GetResponseType(Type request)
        {
            if (!_requestResponse.TryGetValue(request, out Type response))
            {
                throw new Exception($"not found response type, request type: {request.GetType().Name}");
            }

            return response;
        }
    }
}