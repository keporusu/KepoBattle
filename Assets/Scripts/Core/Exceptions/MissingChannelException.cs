using System;

namespace Exceptions
{
    public class MissingChannelException : Exception
    {
        public MissingChannelException(string tag, string gameObjectName)
            : base($"タグ \"{tag}\" のチャンネルが {gameObjectName} の子オブジェクトに見つかりません") { }
    }

}
