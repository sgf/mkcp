using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace mkcp {
    public sealed class SingleThreadSynchronizationContext : SynchronizationContext {
        public SingleThreadSynchronizationContext() {
            m_queue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();
        }

        private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> m_queue;


        public override void Post(SendOrPostCallback d, object state) {
            m_queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
        }

        public void RunOnCurrentThread() {
            KeyValuePair<SendOrPostCallback, object> workItem;
            while (m_queue.TryTake(out workItem, Timeout.Infinite))
                workItem.Key(workItem.Value);

        }


        public void Complete() { m_queue.CompleteAdding(); }

    }
}
