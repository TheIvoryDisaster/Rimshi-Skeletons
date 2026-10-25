# Skeleton workbench and parts

Matthew's spec, 2026-10-25. Skeletons replace lost or worn parts with skeleton parts made at their own workbench, from a chain of intermediates based on Kenshi's robotics components (https://kenshi.fandom.com/wiki/Robotics_Components) and robot limbs (https://kenshi.fandom.com/wiki/Robot_Limbs). Vanilla prosthetics stay locked on skeletons (Skeleton/ProstheticsLock.cs); these parts are the replacement.

## Workbench

`Rimshi_SkeletonWorkbench`, a 2x1 powered bench (300 W) with two rail-mounted claw arms (art proposal C, docs/art/proposals/workbench/). Built from 150 steel and 6 components. It's minifiable through vanilla's BenchBase. The skeleton repair bed was already minifiable (FurnitureBase). It makes the intermediates, every skeleton part and the repair kits, which moved from the machining table.

## Research

| Project | Needs | Unlocks |
| --- | --- | --- |
| Skeleton fabrication (800) | Basic mechtech | Workbench, intermediates, economy and standard limbs, optical sensor, audio receiver |
| Advanced skeleton parts (1600, hi-tech bench) | Skeleton fabrication | KLR, industrial and steady limbs, AI core |
| Skeleton repair kits (600) | Basic mechtech and skeleton fabrication | Repair kits (now made at the workbench) |

## Intermediates

Made at the workbench with Crafting. While being made, they show as vanilla's unfinished component. The work shown in game is ticks / 60.

| Item | Cost | Work | Crafting |
| --- | --- | --- | --- |
| Motor | 1 component, 20 steel | 100 | 6 |
| Robotic component | 2 components, 30 steel, 10 silver | 150 | 8 |
| Skeleton muscle | 2 components, 30 steel, 10 plasteel | 200 | 8 |

Vanilla components and advanced components are also used. The placeholder art is the vanilla advanced component recoloured black with caution-yellow trim, until Matthew picks from the ComfyUI proposals.

## Parts

All parts need Crafting 11 and are made at the workbench. Each install surgery uses one repair kit instead of medicine (skeletons take none), has no anaesthetic, and is offered only on skeletons (Recipe_Rimshi_InstallSkeletonPart). Parts and intermediates never burn. The placeholder art is vanilla's body part item recoloured brown with a grey seam.

| Tier | Cost (arm and leg alike) | Work | Effect |
| --- | --- | --- | --- |
| Economy | 1 motor, 1 robotic component, 20 steel | 50 | 80% efficiency |
| Skeleton (standard) | 2 motors, 1 robotic component, 1 skeleton muscle, 40 steel | 65 | 100% |
| KLR | 2 motors, 1 robotic component, 1 skeleton muscle, 120 steel, 30 uranium | 80 | 100%, takes 20% less damage |
| Industrial | 4 motors, 1 robotic component, 2 skeleton muscle, 30 uranium | 90 | Leg 115%, arm 108% and +10% melee damage each; 8% less damage |
| Steady | 3 motors, 4 robotic components, 2 skeleton muscle, 10 plasteel | 100 | Leg +8 melee dodge points; arm 115% and a 10% chance per hit to disarm |
| Optical sensor (eye), audio receiver (ear) | 2 robotic components, 2 components, 1 advanced component | 80 | 100% |
| AI core (brain) | 2 robotic components, 2 advanced components, 1 AI persona core | 100 | 100%; installed over a damaged core, it repairs all damage |

Notes:
- Efficiency over 100% uses vanilla's partEfficiency, the way the bionic arm's 125% works. So "+15% moving" for a leg is 115% efficiency on that leg.
- Melee damage (MeleeDamageFactor) is pawn-wide in vanilla. That's why each industrial arm adds 10%, so a pair gives the specified 20%. It scales damage per hit (strength), not attack speed.
- Melee dodge chance offsets are in the stat's own points, before its curve (vanilla Nimble is +15; 5 points is 0%, 20 is 30%). A literal +0.3 would do nothing, so each steady leg gives +8 and a pair is about Nimble.
- Damage resistance: a prefix on DamageWorker_AddInjury.FinalizeAndAddInjury(float) scales the damage of any injury that lands on the part or anything attached below it (an arm's hand and fingers).
- Disarm: a postfix on Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget. After a hit that does damage, each steady arm has a 10% chance (combined, about 19% for a pair) to make the target drop its primary weapon.
- AI core: an added part on the brain. Destroying it still kills, as vanilla does for the brain. Permanent brain damage for skeletons is planned for later. The repair already removes every injury, scar and other harmful condition on the core, and the old core stays in place.
- Skeletons are already 0% flammable (Flammability factor 0 on the metal chassis gene). The workbench, intermediates, parts and repair kits are now 0% too.

## Open

- Final art for the intermediates: 3 ComfyUI proposals each in docs/art/proposals/intermediates/, waiting for Matthew's pick.
- The repair kit's new cost (1 robotic component, 5 steel, Proposed:) raises its price. It used 1 component.
- Install surgery uses Medicine like vanilla. It could use Crafting like the repair job.
