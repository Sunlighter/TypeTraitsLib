using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Sunlighter.TypeTraitsLib.Building
{
    public interface IBuildRule<K, V>
#if !NETSTANDARD2_0
        where K : notnull
#endif
    {
        bool CanBuild(K k);

        ImmutableSortedSet<K> GetPrerequisites(K k);

        V Build(K k, ImmutableSortedDictionary<K, V> prerequisites);
    }

    public interface IAbstractBuilderServices<K, V>
#if !NETSTANDARD2_0
        where K : notnull
#endif
    {
        Exception NewException_CannotBuild(ImmutableList<IBuildRule<K, V>> rules, ImmutableSortedDictionary<K, ImmutableSortedSet<int>> unbuildables);

        Exception NewException_CannotBuild(ImmutableSortedSet<K> unbuildables);

        V CreateFixup(K key);

        void SetFixup(K key, V fixup, V value);
    }

    public static class AbstractBuilder
    {
        [Obsolete("Use Build2")]
        public static ImmutableSortedDictionary<K, V> Build<K, V>
        (
            IAbstractBuilderServices<K, V> services,
            ImmutableSortedDictionary<K, V> existingItems,
            ImmutableList<IBuildRule<K, V>> rules,
            ImmutableSortedSet<K> targets
        )
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            ImmutableSortedSet<K> emptyKeySet = targets.Clear();
            ImmutableSortedSet<K> desires = targets;
            ImmutableSortedDictionary<K, ImmutableSortedSet<int>> unbuildables = ImmutableSortedDictionary<K, ImmutableSortedSet<int>>.Empty.WithComparers(existingItems.KeyComparer);
            ImmutableSortedDictionary<K, int> buildables = ImmutableSortedDictionary<K, int>.Empty.WithComparers(existingItems.KeyComparer);
            ImmutableSortedDictionary<K, ImmutableSortedSet<K>> allRequirements = ImmutableSortedDictionary<K, ImmutableSortedSet<K>>.Empty.WithComparers(existingItems.KeyComparer);

            while (desires.Count > 0)
            {
                K desire = desires[0];
                desires = desires.Remove(desire);
                if (existingItems.ContainsKey(desire))
                {
                    // continue;
                }
                else if (buildables.ContainsKey(desire))
                {
                    // continue
                }
                else
                {
                    ImmutableSortedSet<int> applicableRules = Enumerable.Range(0, rules.Count).Where(i => rules[i].CanBuild(desire)).ToImmutableSortedSet();

                    if (applicableRules.Count != 1)
                    {
                        unbuildables = unbuildables.Add(desire, applicableRules);
                    }
                    else
                    {
                        int index = applicableRules[0];
                        IBuildRule<K, V> rule = rules[index];
                        buildables = buildables.Add(desire, index);
                        ImmutableSortedSet<K> prereqs = rule.GetPrerequisites(desire);
                        allRequirements = allRequirements.Add(desire, prereqs);
                        desires = desires.Union(prereqs);
                    }
                }
            }

            if (unbuildables.Count > 0)
            {
                throw services.NewException_CannotBuild(rules, unbuildables);
            }

            while(true)
            {
                bool changed = false;
                ImmutableSortedDictionary<K, ImmutableSortedSet<K>> requirements2 = allRequirements.Clear();

                foreach(K k in allRequirements.Keys)
                {
                    ImmutableSortedSet<K> newSet = allRequirements[k].UnionAll(allRequirements[k].Where(u => allRequirements.ContainsKey(u)).Select(u => allRequirements[u]));
                    requirements2 = requirements2.Add(k, newSet);
                    changed = changed || (newSet.Count != allRequirements[k].Count);
                }

                if (changed)
                {
                    allRequirements = requirements2;
                }
                else break;
            }

            ImmutableSortedDictionary<K, V> fixups = existingItems.Clear();

            foreach (K k in allRequirements.Keys)
            {
                if (allRequirements[k].Contains(k))
                {
                    fixups = fixups.Add(k, services.CreateFixup(k));
                }
            }

            while (buildables.Count > 0)
            {
                ImmutableSortedDictionary<K, int> nextTime = buildables.Clear();

                foreach (KeyValuePair<K, int> buildable in buildables)
                {
                    K desire = buildable.Key;
                    int ruleIndex = buildable.Value;
                    IBuildRule<K, V> rule = rules[ruleIndex];

                    ImmutableSortedDictionary<K, V> prerequisites = existingItems.Clear();

                    bool canBuild = true;

                    foreach (K k in rule.GetPrerequisites(desire))
                    {
                        if (existingItems.ContainsKey(k))
                        {
                            prerequisites = prerequisites.Add(k, existingItems[k]);
                        }
                        else if (fixups.ContainsKey(k))
                        {
                            prerequisites = prerequisites.Add(k, fixups[k]);
                        }
                        else
                        {
                            canBuild = false;
                            nextTime = nextTime.Add(desire, ruleIndex);
                            break;
                        }
                    }

                    if (canBuild)
                    {
                        V result = rule.Build(desire, prerequisites);

                        if (fixups.ContainsKey(desire))
                        {
                            services.SetFixup(desire, fixups[desire], result);
                            fixups = fixups.Remove(desire);
                        }

                        existingItems = existingItems.Add(desire, result);
                    }
                }

                if (buildables.Count == nextTime.Count) throw new BuilderException("No progress (this should not be possible)");

                buildables = nextTime;
            }

            return existingItems;
        }

        private class BuildPlan<K>
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            private readonly ImmutableSortedSet<K> desires;
            private readonly ImmutableSortedSet<K> unbuildables;
            private readonly ImmutableSortedDictionary<K, int> buildables;
            private readonly ImmutableSortedDictionary<K, ImmutableSortedSet<K>> allRequirements;

            public BuildPlan
            (
                ImmutableSortedSet<K> desires,
                ImmutableSortedSet<K> unbuildables,
                ImmutableSortedDictionary<K, int> buildables,
                ImmutableSortedDictionary<K, ImmutableSortedSet<K>> allRequirements
            )
            {
                this.desires = desires;
                this.unbuildables = unbuildables;
                this.buildables = buildables;
                this.allRequirements = allRequirements;
            }

            public ImmutableSortedSet<K> Desires => desires;

            public ImmutableSortedSet<K> Unbuildables => unbuildables;

            public ImmutableSortedDictionary<K, int> Buildables => buildables;

            public ImmutableSortedDictionary<K, ImmutableSortedSet<K>> AllRequirements => allRequirements;

            public static ImmutableSortedDictionary<K, V> Execute<V>
            (
                BuildPlan<K> plan,
                IAbstractBuilderServices<K, V> services,
                ImmutableSortedDictionary<K, V> existingItems,
                ImmutableList<IBuildRule<K, V>> rules,
                ImmutableSortedSet<K> targets
            )
            {
                ImmutableSortedSet<K> emptyKeySet = targets.Clear();

                ImmutableSortedSet<K> desires = plan.Desires;
                ImmutableSortedSet<K> unbuildables = plan.Unbuildables;
                ImmutableSortedDictionary<K, int> buildables = plan.Buildables;
                ImmutableSortedDictionary<K, ImmutableSortedSet<K>> allRequirements = plan.AllRequirements;

                if (unbuildables.Count > 0)
                {
                    throw services.NewException_CannotBuild(unbuildables);
                }

                while (true)
                {
                    bool changed = false;
                    ImmutableSortedDictionary<K, ImmutableSortedSet<K>> requirements2 = allRequirements.Clear();

                    foreach (K k in allRequirements.Keys)
                    {
                        ImmutableSortedSet<K> newSet = allRequirements[k].UnionAll(allRequirements[k].Where(u => allRequirements.ContainsKey(u)).Select(u => allRequirements[u]));
                        requirements2 = requirements2.Add(k, newSet);
                        changed = changed || (newSet.Count != allRequirements[k].Count);
                    }

                    if (changed)
                    {
                        allRequirements = requirements2;
                    }
                    else break;
                }

                ImmutableSortedDictionary<K, V> fixups = existingItems.Clear();

                foreach (K k in allRequirements.Keys)
                {
                    if (allRequirements[k].Contains(k))
                    {
                        fixups = fixups.Add(k, services.CreateFixup(k));
                    }
                }

                while (buildables.Count > 0)
                {
                    ImmutableSortedDictionary<K, int> nextTime = buildables.Clear();

                    foreach (KeyValuePair<K, int> buildable in buildables)
                    {
                        K desire = buildable.Key;
                        int ruleIndex = buildable.Value;
                        IBuildRule<K, V> rule = rules[ruleIndex];

                        ImmutableSortedDictionary<K, V> prerequisites = existingItems.Clear();

                        bool canBuild = true;

                        foreach (K k in rule.GetPrerequisites(desire))
                        {
                            if (existingItems.ContainsKey(k))
                            {
                                prerequisites = prerequisites.Add(k, existingItems[k]);
                            }
                            else if (fixups.ContainsKey(k))
                            {
                                prerequisites = prerequisites.Add(k, fixups[k]);
                            }
                            else
                            {
                                canBuild = false;
                                nextTime = nextTime.Add(desire, ruleIndex);
                                break;
                            }
                        }

                        if (canBuild)
                        {
                            V result = rule.Build(desire, prerequisites);

                            if (fixups.ContainsKey(desire))
                            {
                                services.SetFixup(desire, fixups[desire], result);
                                fixups = fixups.Remove(desire);
                            }

                            existingItems = existingItems.Add(desire, result);
                        }
                    }

                    if (buildables.Count == nextTime.Count) throw new BuilderException("No progress (this should not be possible)");

                    buildables = nextTime;
                }

                return existingItems;
            }
        }

        public static ImmutableSortedDictionary<K, V> Build2<K, V>
        (
            IAbstractBuilderServices<K, V> services,
            ImmutableSortedDictionary<K, V> existingItems,
            ImmutableList<IBuildRule<K, V>> rules,
            ImmutableSortedSet<K> targets
        )
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            ImmutableSortedSet<K> emptyKeySet = targets.Clear();

            ImmutableList<BuildPlan<K>> plans = ImmutableList<BuildPlan<K>>.Empty.Add
            (
                new BuildPlan<K>
                (
                    targets,
                    emptyKeySet,
                    ImmutableSortedDictionary<K, int>.Empty.WithComparers(existingItems.KeyComparer),
                    ImmutableSortedDictionary<K, ImmutableSortedSet<K>>.Empty.WithComparers(existingItems.KeyComparer)
                )
            );

            bool changed = true;
            while(changed)
            {
                changed = false;

                ImmutableList<BuildPlan<K>> newPlans = ImmutableList<BuildPlan<K>>.Empty;

                foreach(BuildPlan<K> plan in plans)
                {
                    if (plan.Desires.IsEmpty)
                    {
                        newPlans = newPlans.Add(plan);
                    }
                    else
                    {
                        changed = true;
                        K desire = plan.Desires[0];
                        ImmutableSortedSet<K> newDesires = plan.Desires.Remove(desire);
                        if (existingItems.ContainsKey(desire))
                        {
                            newPlans = newPlans.Add(new BuildPlan<K>(newDesires, plan.Unbuildables, plan.Buildables, plan.AllRequirements));
                        }
                        else if (plan.Buildables.ContainsKey(desire))
                        {
                            newPlans = newPlans.Add(new BuildPlan<K>(newDesires, plan.Unbuildables, plan.Buildables, plan.AllRequirements));
                        }
                        else
                        {
                            ImmutableSortedSet<int> applicableRules = Enumerable.Range(0, rules.Count).Where(i => rules[i].CanBuild(desire)).ToImmutableSortedSet();

                            if (applicableRules.IsEmpty)
                            {
                                ImmutableSortedSet<K> newUnbuildables = plan.Unbuildables.Add(desire);
                                newPlans = newPlans.Add(new BuildPlan<K>(newDesires, newUnbuildables, plan.Buildables, plan.AllRequirements));
                            }
                            else
                            {
                                foreach(int index in applicableRules)
                                {
                                    IBuildRule<K, V> rule = rules[index];
                                    ImmutableSortedDictionary<K, int> newBuildables = plan.Buildables.Add(desire, index);
                                    ImmutableSortedSet<K> prereqs = rule.GetPrerequisites(desire);
                                    ImmutableSortedDictionary<K, ImmutableSortedSet<K>> newAllRequirements = plan.AllRequirements.Add(desire, prereqs);
                                    ImmutableSortedSet<K> newDesires2 = newDesires.Union(prereqs);

                                    newPlans = newPlans.Add(new BuildPlan<K>(newDesires2, plan.Unbuildables, newBuildables, newAllRequirements));
                                }
                            }
                        }
                    }
                }

                plans = newPlans;
            }

            ImmutableList<Exception> excList = ImmutableList<Exception>.Empty;

            foreach(BuildPlan<K> plan in plans)
            {
                try
                {
                    return BuildPlan<K>.Execute(plan, services, existingItems, rules, targets);
                }
                catch(Exception exc)
                {
                    excList = excList.Add(exc);
                }
            }

            throw new AggregateException(excList);
        }
    }
}