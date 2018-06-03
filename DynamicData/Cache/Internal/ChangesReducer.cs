using System.Diagnostics.Contracts;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class ChangesReducer
    {
        [Pure]
        public static Optional<Change<TObject, TKey>> Reduce<TObject, TKey>(
            Change<TObject, TKey> previous,
            Change<TObject, TKey> next)
        {
            if (previous.Reason == ChangeReason.Add && next.Reason == ChangeReason.Remove)
            {
                return Optional<Change<TObject, TKey>>.None;
            } 
            else if (previous.Reason == ChangeReason.Remove && next.Reason == ChangeReason.Add)
            {
                return Optional.Some(
                    new Change<TObject, TKey>(ChangeReason.Update, next.Key, next.Current, previous.Current,
                        next.CurrentIndex, previous.CurrentIndex)
                );
            }
            else if (previous.Reason == ChangeReason.Add && next.Reason == ChangeReason.Update)
            {
                return Optional.Some(new Change<TObject, TKey>(ChangeReason.Add, next.Key, next.Current, next.CurrentIndex));
            }
            else if (previous.Reason == ChangeReason.Update && next.Reason == ChangeReason.Update)
            {
                return Optional.Some(
                   new Change<TObject, TKey>(ChangeReason.Update, previous.Key, next.Current, previous.Previous,
                        next.CurrentIndex, previous.PreviousIndex)
                );
            }
            else
            {
                return next;
            }
        }
    }
}
