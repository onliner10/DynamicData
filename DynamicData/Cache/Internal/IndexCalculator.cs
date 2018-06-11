using DynamicData.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Cache.Internal
{
    /// <summary>
    /// Calculates a sequential change set.
    /// 
    /// This enables the binding infrastructure to simply iterate the change set
    /// and apply indexed changes with no need to apply ant expensive IndexOf() operations.
    /// </summary>
    internal sealed class IndexCalculator<TObject, TKey>
    {
        private KeyValueComparer<TObject, TKey> _comparer;
        private LinkedList<KeyValuePair<TKey, TObject>> _list;

        private readonly SortOptimisations _optimisations;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public IndexCalculator(KeyValueComparer<TObject, TKey> comparer, SortOptimisations optimisations)
        {
            _comparer = comparer;
            _optimisations = optimisations;
            _list = new LinkedList<KeyValuePair<TKey, TObject>>();
        }

        public IEnumerable<Change<TObject, TKey>> ReduceChanges(IEnumerable<Change<TObject, TKey>> input)
        {
            return input
                .GroupBy(kvp=>kvp.Key)
                .Select(g => {
                    return g.Aggregate(Optional<Change<TObject, TKey>>.None, (acc, kvp) => ChangesReducer.Reduce(acc, kvp));
                })
                .Where(x=>x.HasValue)
                .Select(x=>x.Value);
        }

        /// <summary>
        /// Initialises the specified changes.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        public IChangeSet<TObject, TKey> Load(ChangeAwareCache<TObject, TKey> cache)
        {
            //for the first batch of changes may have arrived before the comparer was set.
            //therefore infer the first batch of changes from the cache
            _list = new LinkedList<KeyValuePair<TKey, TObject>>(cache.KeyValues.OrderBy(kv => kv, _comparer));
            var initialItems = _list.Select((t, index) => new Change<TObject, TKey>(ChangeReason.Add, t.Key, t.Value, index));
            return new ChangeSet<TObject, TKey>(initialItems);
        }

        /// <summary>
        /// Initialises the specified changes.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        public void Reset(ChangeAwareCache<TObject, TKey> cache)
        {
            _list = new LinkedList<KeyValuePair<TKey, TObject>>(cache.KeyValues.OrderBy(kv => kv, _comparer));
        }

        public IChangeSet<TObject, TKey> ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
        {
            _comparer = comparer;
            return ChangeSet<TObject, TKey>.Empty;
        }

        public IChangeSet<TObject, TKey> Reorder()
        {
            var result = new List<Change<TObject, TKey>>();

            if (_optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
            {
                //reorder entire sequence and do not calculate moves
                _list = new LinkedList<KeyValuePair<TKey, TObject>>(_list.OrderBy(kv => kv, _comparer));
            }
            else
            {
                var sorted = _list.OrderBy(t => t, _comparer).Select((item, i) => Tuple.Create(item, i)).ToList();
                var oldByKey = _list.Select((item, i) => Tuple.Create(item, i)).ToDictionary(x => x.Item1.Key, x => x.Item2);

                foreach (var item in sorted)
                {
                    var currentItem = item.Item1;
                    var currentIndex = item.Item2;

                    var previousIndex = oldByKey[currentItem.Key];

                    if (currentIndex != previousIndex)
                    {
                        result.Add(new Change<TObject, TKey>(currentItem.Key, currentItem.Value, currentIndex, previousIndex));
                    }
                }

                _list = new LinkedList<KeyValuePair<TKey, TObject>>(sorted.Select(s => s.Item1));
            }

            return new ChangeSet<TObject, TKey>(result);
        }
        
        //private IEnumerable<Change<TObject, TKey>> ApplyRemovals(IEnumerable<Change<TObject, TKey>> changes)
        //{
        //    var result = new List<Change<TObject, TKey>>(changes.Count());
        //    var changeQueue = new Queue<Change<TObject, TKey>>(changes);
        //    if(changeQueue.Any())
        //    {
        //        var index = 0;
        //        var node = _list.First;
        //        var nextChange = changeQueue.Dequeue();
        //        while (node != null)
        //        {
        //            var areequal = EqualityComparer<TKey>.Default.Equals(node.Value.Key, nextChange.Key);
        //            if (areequal)
        //            {
        //                _list.Remove(node);
        //                result.Add(new Change<TObject, TKey>(ChangeReason.Remove, nextChange.Key, nextChange.Current, index));
        //                if (!changeQueue.Any()) break;

        //                nextChange = changeQueue.Dequeue();
        //            }
        //            node = node.Next;
        //            index++;
        //        }
        //    }

        //    return result;
        //}

        /// <summary>
        /// Dynamic calculation of moved items which produce a result which can be enumerated through in order
        /// </summary>
        /// <returns></returns>
        public IChangeSet<TObject, TKey> Calculate(IChangeSet<TObject, TKey> changes)
        {
            var reducedChanges = ReduceChanges(changes).ToList();
            var result = new List<Change<TObject, TKey>>(reducedChanges.Count);
            var refreshes = new List<Change<TObject, TKey>>(changes.Refreshes);

            var removes = new Queue<Change<TObject, TKey>>(reducedChanges.Where(c => c.Reason == ChangeReason.Remove).OrderBy(r=>new KeyValuePair<TKey, TObject>(r.Key, r.Current), _comparer));
            //result.AddRange(ApplyRemovals(removes));
            if (removes.Any())
            {
                var index = 0;
                var node = _list.First;
                var nextToBeRemoved = removes.Dequeue();
                while (node != null)
                {
                    var areequal = EqualityComparer<TKey>.Default.Equals(node.Value.Key, nextToBeRemoved.Key);
                    if (areequal)
                    {
                        var nodeCopy = node;
                        node = node.Next;

                        _list.Remove(nodeCopy);
                        result.Add(new Change<TObject, TKey>(ChangeReason.Remove, nextToBeRemoved.Key, nextToBeRemoved.Current, index));
                        if (!removes.Any()) break;

                        nextToBeRemoved = removes.Dequeue();
                    } else
                    {
                        node = node.Next;
                        index++;
                    }
                }
            }

            var adds = new Queue<Change<TObject, TKey>>(reducedChanges.Where(c => c.Reason == ChangeReason.Add).OrderBy(r => r.CurrentIndex));
            if (adds.Any())
            {
                var index = 0;
                var node = _list.First;
                var nodeToBeAdded = adds.Peek();
                var kvp = new KeyValuePair<TKey, TObject>(nodeToBeAdded.Key, nodeToBeAdded.Current);
                while (node != null)
                {
                    var shouldInsert = _comparer.Compare(node.Value, kvp) > 0;
                    if (shouldInsert)
                    {
                        var nodeToAdd = new LinkedListNode<KeyValuePair<TKey, TObject>>(kvp);
                        _list.AddBefore(node, nodeToAdd);
                        result.Add(new Change<TObject, TKey>(ChangeReason.Add, nodeToBeAdded.Key, nodeToBeAdded.Current, index));

                        node = nodeToAdd;

                        adds.Dequeue();
                        if (!adds.Any()) break;
                        
                        nodeToBeAdded = adds.Peek();
                        kvp = new KeyValuePair<TKey, TObject>(nodeToBeAdded.Key, nodeToBeAdded.Current);
                    }
                    node = node.Next;
                    index++;
                }

                if(adds.Any())
                {
                    _list.AddLast(kvp);
                    result.Add(new Change<TObject, TKey>(ChangeReason.Add, nodeToBeAdded.Key, nodeToBeAdded.Current, index));
                }
            }

            var updateChanges = reducedChanges.Where(c => c.Reason == ChangeReason.Update).OrderBy(r => new KeyValuePair<TKey, TObject>(r.Key, r.Current), _comparer);
            var updates = new Queue<Change<TObject, TKey>>(updateChanges);

            var updateRemovals = new Dictionary<KeyValuePair<TKey, TObject>, Tuple<int, KeyValuePair<TKey, TObject>>>();
            var keysToBeRemoved = new SortedDictionary<TKey, TObject>(updateChanges.ToDictionary(x => x.Key, x => x.Current));

            if (updates.Any())
            {
                var index = 0;
                var node = _list.First;
                while (node != null)
                {
                    if(keysToBeRemoved.ContainsKey(node.Value.Key))
                    {
                        var kvp = new KeyValuePair<TKey, TObject>(node.Value.Key, keysToBeRemoved[node.Value.Key]);

                        var nodeCopy = node;
                        node = node.Next;
                        _list.Remove(nodeCopy);
                        updateRemovals[kvp] = Tuple.Create(index, nodeCopy.Value);

                        updates.Dequeue();
                        if (!updates.Any()) break;
                    }
                    else {
                        node = node.Next;
                    }
                    index++;
                }
            }

            updates = new Queue<Change<TObject, TKey>>(updateChanges);

            if (updates.Any())
            {
                var index = 0;
                var offset = 0;
                var node = _list.First;

                var nodeToBeUpdated = updates.Peek();
                var kvp = new KeyValuePair<TKey, TObject>(nodeToBeUpdated.Key, nodeToBeUpdated.Current);
                while (node != null)
                {
                    var shouldInsertElement = _comparer.Compare(node.Value, kvp) >= 0;
                    if (shouldInsertElement)
                    {
                        var previous = updateRemovals[kvp];
                        var previousIndex = previous.Item1 + offset;

                        var nodeToAdd = new LinkedListNode<KeyValuePair<TKey, TObject>>(kvp);
                        _list.AddBefore(node, nodeToAdd);
                        if(previousIndex == index)
                        {
                            result.Add(new Change<TObject, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, previous.Item2.Value, index, previousIndex));
                        } else
                        {
                            offset++;
                            result.Add(new Change<TObject, TKey>(kvp.Key, kvp.Value, index, previousIndex));
                            result.Add(new Change<TObject, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, previous.Item2.Value, index, index));
                        }

                        node = nodeToAdd;
                        updates.Dequeue();
                        if (!updates.Any()) break;
                        
                        nodeToBeUpdated = updates.Peek();
                        kvp = new KeyValuePair<TKey, TObject>(nodeToBeUpdated.Key, nodeToBeUpdated.Current);
                    }
                    node = node.Next;
                    index++;
                }

                if(updates.Any())
                {
                    var previous = updateRemovals[kvp];
                    var previousIndex = previous.Item1 + offset;

                    _list.AddLast(kvp);
                    if(previousIndex == index - 1)
                    {
                        result.Add(new Change<TObject, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, previous.Item2.Value, index - 1, previousIndex));
                    } else
                    {
                        result.Add(new Change<TObject, TKey>(kvp.Key, kvp.Value, index - 1, previousIndex));
                        result.Add(new Change<TObject, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, previous.Item2.Value, index - 1, index - 1));
                    }
                }
            }

            return new ChangeSet<TObject, TKey>(result);
        }

        public IComparer<KeyValuePair<TKey, TObject>> Comparer => _comparer;

        public LinkedList<KeyValuePair<TKey, TObject>> List => _list;

     
    }
}
