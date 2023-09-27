using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace AppleOfEden
{
    public class AppleModule : ItemModule
    {
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<AppleComponent>();
        }
    }
    public class AppleComponent : MonoBehaviour
    {
        Item item;
        EffectData boltEffect;
        EffectData soundEffect;
        ItemData lineData;
        float cooldown = 0;
        CreatureData creatureData;
        public void Start()
        {
            item = GetComponent<Item>();
            item.OnHeldActionEvent += Item_OnHeldActionEvent;
            boltEffect = Catalog.GetData<EffectData>("AppleOfEdenBolt");
            soundEffect = Catalog.GetData<EffectData>("AppleOfEdenSound");
            lineData = Catalog.GetData<ItemData>("AppleOfEdenLines");
        }

        private void Item_OnHeldActionEvent(RagdollHand ragdollHand, Handle handle, Interactable.Action action)
        {
            if (action == Interactable.Action.UseStart && Time.time - cooldown >= 3f)
            {
                cooldown = Time.time;
                EffectInstance effectInstance = soundEffect.Spawn(item.transform, null, false);
                effectInstance.SetIntensity(1f);
                effectInstance.Play();
                lineData.SpawnAsync(item =>
                {
                    item.Despawn(5f);
                }, item.transform.position, item.transform.rotation);
                foreach (Creature creature in GetCreatures())
                {
                    if (creature != null)
                    {
                        ShootBolt(item.transform, creature.ragdoll.headPart.transform);
                        if (Vector3.Distance(creature.ragdoll.headPart.transform.position, item.transform.position) <= 2)
                        {
                            creature.Kill();
                        }
                        else
                        {
                            int random = UnityEngine.Random.Range(0, 2);
                            switch (random)
                            {
                                case 0:
                                    Insanity(creature);
                                    break;
                                case 1:
                                    Flee(creature);
                                    break;
                            }
                        }
                    }
                }
            }
            else if (action == Interactable.Action.AlternateUseStart && Time.time - cooldown >= 2f)
            {
                cooldown = Time.time;
                Creature holder = item.mainHandler.creature;
                creatureData = Catalog.GetData<CreatureData>(holder.data.gender == CreatureData.Gender.Male ? "EdenMale" : "EdenFemale");
                if (holder != null)
                    creatureData.SpawnAsync(holder.transform.position - holder.ragdoll.headPart.transform.forward, holder.ragdoll.headPart.transform.rotation.y, null, true, null, creature =>
                    {
                        creature.SetFaction(holder.factionId);
                        creature.container.LoadFromPlayerInventory();
                        List<Item> items = creature.equipment.GetHolsterWeapons();
                        foreach(Item other in items)
                        {
                            other.holder.UnSnap(other, true, true);
                            other.Despawn();
                        }
                        items.Clear();
                        foreach (Item other in holder.equipment.GetHolsterWeapons())
                        {
                            ItemData data = other.data;
                            ContentState contentState = other.contentState;
                            ContentState state = contentState != null ? contentState.CloneJson() : null;
                            List<ContentCustomData> contentCustomData = other.contentCustomData;
                            List<ContentCustomData> customDataList = contentCustomData != null ? contentCustomData.CloneJson() : null;
                            creature.container.AddContent(data, state, customDataList);
                        }
                        List<ContainerData.Content> contents = new List<ContainerData.Content>();
                        foreach(ContainerData.Content content in creature.container.contents)
                        {
                            if (content.itemData != null && content.itemData.HasModule<ItemModuleSpell>())
                            {
                                contents.Add(content);
                            }
                        }
                        foreach(ContainerData.Content content in contents)
                        {
                            creature.container.RemoveContent(content);
                        }
                        contents.Clear();
                        creature.equipment.EquipAllWardrobes(false);
                        creature.mana.maxMana = holder.mana.maxMana;
                        creature.mana.currentMana = creature.mana.maxMana;
                        creature.maxHealth = holder.maxHealth * 0.1f;
                        creature.currentHealth = creature.maxHealth;
                    });
            }
        }

        public void ShootBolt(
            Transform source = null,
            Transform target = null)
        {
            EffectInstance effectInstance = boltEffect.Spawn(Vector3.zero, Quaternion.identity);
            effectInstance.SetSource(source);
            effectInstance.SetTarget(target);
            effectInstance.Play();
        }
        public List<Creature> GetCreatures()
        {
            List<Creature> creatures = new List<Creature>();
            foreach(Creature creature in Creature.allActive)
            {
                if(!creature.isKilled && Vector3.Distance(creature.ragdoll.headPart.transform.position, item.transform.position) <= 10 && creature.brain.instance != null && !creatures.Contains(creature) && creature != item.mainHandler.creature
                     && creature.faction != item.mainHandler?.creature.faction)
                {
                    creatures.Add(creature);
                }
            }
            return creatures;
        }
        public Creature GetClosestCreature(Creature creature)
        {
            Creature closestCreature = null;
            foreach(Creature enemy in Creature.allActive)
            {
                if (!enemy.isKilled && enemy != item.mainHandler?.creature && enemy != creature && (closestCreature == null || Vector3.Distance(enemy.ragdoll.targetPart.transform.position, creature.ragdoll.targetPart.transform.position) <
                    Vector3.Distance(closestCreature.ragdoll.targetPart.transform.position, creature.ragdoll.targetPart.transform.position)))
                    closestCreature = enemy;
            }
            return closestCreature;
        }
        public int CreatureCount()
        {
            int count = 0;
            foreach (Creature creature in Creature.allActive)
            {
                if (!creature.isKilled && creature != item.mainHandler?.creature && creature.faction != item.mainHandler?.creature.faction)
                {
                    count++;
                }
            }
            return count;
        }
        public void Insanity(Creature creature)
        {
            if((creature.factionId != 0 || CreatureCount() > 1) && !creature.brain.instance.tree.blackboard.Find<bool>("ForceFlee"))
            {
                creature.SetFaction(0);
                if (creature.brain.currentTarget == item.mainHandler?.creature)
                    creature.brain.currentTarget = GetClosestCreature(creature);
            }
            else
            {
                if (Vector3.Distance(creature.ragdoll.headPart.transform.position, item.transform.position) <= 2)
                {
                    creature.Kill();
                }
                else Flee(creature);
            }
        }
        public void Flee(Creature creature)
        {
            if(!creature.brain.instance.tree.blackboard.Find<bool>("ForceFlee"))
            {
                creature.SetFaction(-1);
                creature.brain.instance.tree.blackboard.UpdateVariable<bool>("ForceFlee", true);
                if (WaveSpawner.TryGetRunningInstance(out WaveSpawner waveSpawner))
                {
                    if (waveSpawner.spawnedCreatures.Contains(creature))
                        waveSpawner.spawnedCreatures.Remove(creature);
                    creature.spawnGroup = null;
                }
                if (creature.handLeft.grabbedHandle != null)
                    creature.handLeft.UnGrab(false);
                if (creature.handRight.grabbedHandle != null)
                    creature.handRight.UnGrab(false);
            }
            else
            {
                creature.Kill();
            }
        }
    }
}
