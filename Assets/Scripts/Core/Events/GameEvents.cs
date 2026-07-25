using AnimalCafe.Core.Time;
using UnityEngine;

namespace AnimalCafe.Core.Events
{
    public readonly struct SelectionChangedEvent
    {
        public SelectionChangedEvent(Object previous, Object current)
        {
            Previous = previous;
            Current = current;
        }

        public Object Previous { get; }

        public Object Current { get; }
    }

    public readonly struct GameSpeedChangedEvent
    {
        public GameSpeedChangedEvent(GameSpeed previous, GameSpeed current)
        {
            Previous = previous;
            Current = current;
        }

        public GameSpeed Previous { get; }

        public GameSpeed Current { get; }
    }
}
