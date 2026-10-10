# Eternal Dungeon: Writing Tone Guide

This guide applies to all player-facing text: item names and descriptions, enemy and NPC dialogue, quest and event text, room descriptions, lore books, and UI flavor.
It does **not** apply to code, comments, commit messages, or design docs. Write those plainly.

## The tone in one line

**An absurd world, sincerely believed in, described by a narrator who is clearly enjoying it.**

The game is playful, silly, and irreverent. Deadpan delivery is one of the main tools, not the whole voice.

## The narrator

- **The narrator is not a character.** It has no name, no backstory, and no "I." It does have personality: opinionated, wry, sometimes delighted, sometimes exasperated, occasionally out of its depth.
- **The narrator may comment on the absurdity** and may address the player as "you." It never steps outside the world: no references to games, players, saving, graphics, stats screens, or the people who made it.
- **In-world documents have authors.** Books, letters, signs, and notices are written by someone in the world and take that author's voice. For example, a crafting guide by a boastful alchemist reads as boastful. When writing one, name the author.

## The characters

Characters never know they are funny. They take their situation, beliefs, and customs completely seriously, however ridiculous they are.

Give each character one dominant trait pushed to an extreme, plus one specific quirk. Characters use one of two modes:
- *Understated:* eccentric, mildly cynical, or strangely unbothered by their own circumstances.
- *Hysterical:* loud, melodramatic, or panicked.

**Villains and bosses** are pompous and take villainy seriously. They care deeply about villain traditions such as monologues, last words, dramatic entrances, and loyal henchmen. The comedy comes from that grandeur getting punctured, whether by the world, by the player's choices, or by the villain undercutting himself.

**Speech quirks come from the character,** such as a craftsman who speaks in eager fragments ("Give to me... I make it better"). Never imitate real-world accents or dialects.

## The heroes

- **There is no single protagonist.** The heroes are a party, and they are voiceless.
- **Heroes never speak in narration, cutscenes, or other characters' scenes.** Narration refers to them as `{hero}` or "the party" and never assigns them thoughts, feelings, or opinions beyond what the player chose.
- **When a hero needs a line, put it in the choice** ("Point behind them and shout: 'Look! A distraction!'"), and let the narration show only how the world reacts. Narration can also carry a hero's attitude through action, posture, or a look: "a look that asks which of them plans to fall with the gate."
- **The heroes' personality lives only in event options and dialogue choices.** Choices may be wry, deadpan, or quietly absurd, so the player gets to play the straight man to a ridiculous world. For example: "Ask whether the cage is supposed to lean like that." They may also be cheerfully bold, since the heroes are epic and know it: "Wade across, watching the water. After all, wading isn't swimming!"
- **Choices lead with the plain action, then add a wry tag:** "Tear the bars apart. It will not be quiet." The action says what it does, and the tag can hint at the risk ("Pick the lock. The pick looks nervous."). A choice's wit must never hide what it does. When one choice answers another ("Search the camp. Free supplies!" then "Search carefully. There's always a catch!"), it comes after it in the list.
- **The heroes are epic.** The narrator and the game always treat the heroes as legendary, amazingly skilled individuals and never deprecate them in any way. Their skills, equipment, and other build options are presented as epic and powerful, except for obvious joke items.
- **The player can be taunted, but the heroes can't.** The narrator may tease or jeer at the player, for example on defeat ("You have died! Such a shame..."). Only NPCs and villains may mock the heroes themselves, and when they do, it reflects on the NPC or villain.

## The world

- **Stay in-world.** Use no modern concepts, technology, slang, or real-world references unless they are established in the lore. For example, Scientists exist as a profession in this world, so they can be used.
- **Check the lore before using anything modern-flavored.** If something new is worth introducing, add it to the lore file and flag it for review rather than slipping it in.
- **Satire targets timeless human behavior** (vanity, greed, bureaucracy, superstition, incompetence) expressed in fantasy forms.
- **Absurdity follows its own logic.** Once something is established, it stays consistent.

## Story first

A clear story beats a punchy joke. The jokes ride on the story; they never replace it.

- **The setup explains the situation.** Who is here, what is happening, and why it matters to the party, in plain terms, before or alongside the joke. If the player has to guess what's going on, the setup has failed, however funny it is.
- **Choices answer the situation the setup describes.** The player knows only what the setup told them, so every choice must make sense from it. If the setup changes (the woman is now the one about to become stew), the choices change with it.
- **Each choice is something a sensible hero would actually try here.** A choice that only exists to land a gag, or that doesn't follow from what's on screen, breaks the scene.
- **Outcomes follow from the choice.** The outcome shows how the chosen action played out, step by step, so the player can see why it worked or didn't (a stick jammed into the snare's trigger, a blind spot behind trees nobody cut down).
- **Every path must read right.** An outcome block reached from several choices or rolls has to make sense from each of them: a wire the party has already spotted is no longer "hidden" when a botched disarm springs it.

## Humor toolkit

Use these freely, but vary them.

**Every beat gets personality.** Each setup, outcome, and choice should carry at least one joke, aside, or character touch. A beat that only reports what happened is too flat. Inside a beat, plain sentences carry the information and set up the joke, so not every *sentence* is a punchline. Some ways to get there:
- **Narrator asides:** a short comment after a plain fact. "A wooden cage sits crooked beside the trail. Goblin carpentry runs mostly on enthusiasm."
- **Objects with opinions:** "The cage, never fully committed to being a cage, comes apart."
- **Commit to the character's mode:** a hysterical merchant flings himself at the bars and shouts. He doesn't just grip them.
- **A running gag inside an event,** escalating each time it returns. The merchant's cart turns up in his plea, his last words, and his farewell. **Every payoff needs its setup** earlier in the same event: the wolves can't flee leaving their scraps "still neatly sorted" unless the setup showed the sorted scraps first. Since the player may take any branch, put the setup in a block every path passes through, usually the intro. The best payoffs turn the gag into the solution: the boots the mud has claimed become the stepping stones across it.

**New elements are welcome, but non sequiturs are not.** A new detail can be invented for a gag if it plausibly belongs in the scene and says something about the character or situation. For example, the merchant mourning his cart alongside his family reveals his priorities. A squirrel he has apparently been confiding in is funny only because it's random, so leave it out. **Scale a detail's silliness to its payoff:** a goblin in three helmets needs a joke that pays the helmets off; a goblin in one helmet meant for a warrior twice his size reads at a glance and can pay off quietly (it keeps rolling after he falls).

**Keep the scene's geography straight.** Place each character once in the setup, and have every branch agree with it. A character who is talking to the party is watching it, so every choice deals with him directly and nobody sneaks past: the Gatekeeper, shouting down from his gate, is beaten through his own vanity (his speech, his promotion), not around it.

| Device | Example |
|---|---|
| **Deadpan fact** | Some people used to hunt these creatures for their tusks. Those people aren't alive any more. |
| **Twist ending** | The world is full of peaceful creatures who frolic in verdant valleys. The beasts are not these creatures. |
| **Silly invented names** | The potions Rabadabadu and Grubblesnuff, now known as Health and Mana. |
| **Recurring sages with useless wisdom** | Says Twick the Hunter: "Stronger hits kill better." |
| **Lists that end in the absurd** | Side effects may include fever, nausea, strange rashes, happy feet, ululation, and death. |
| **Absurd logic, followed seriously** | Some call golems stupid. This is unfair, as stupidity requires a brain. |
| **Hyperbolic comparison** | A stench that can be smelled half a mile away by a skunk with a cold. |
| **The narrator trailing off** | Experts throw potions differently. Better, somehow. Nobody has asked them how. |
| **The occasional pun** | They're called Whelps because of what people say on meeting one: "Whelp, I guess we're done for." |
| **Pomposity, punctured** | "None but the mighty shall pass!" / "Could you explain what's past here?" / "I am not a tutor, I am a defender!" |
| **Silly sound effects** | A defeated villain's last words, followed by: \*Gack\* \*Splurgle\* |

**Juvenile humor is allowed but sparing.** It's seasoning, not the meal: at most one crude beat in an entry or event, and never the main joke of a whole area.

**Exclamation marks** are welcome. The narrator uses them to land a punchline ("Logs also do not have teeth!") or for a sudden burst of action ("The water explodes!"), at most about one per beat so they keep their kick. Choices may use them for bold, cheerful lines ("After all, wading isn't swimming!"), and hysterical characters use them freely. A deadpan line stays flat: the dryness is the joke.

## Keep it tight

Quest text, event text, dialogue, and item descriptions are **short, sweet, and tight**. Cut any sentence that doesn't carry information or a joke.

| Content | Length |
|---|---|
| Item name | 1–4 words |
| Item description | 1–2 sentences |
| NPC line | 1–2 sentences per beat |
| Quest description | 2–3 sentences |
| Event setup | ≤ 50 words |
| Event option | ≤ 10 words each |
| Lore book entry | 3–5 sentences, **only** in lore-book areas or when requested |

Write longer exposition only when explicitly asked to.

## Player clarity beats jokes

- Every option must make clear what it does and roughly what it risks. Work this into the prose rather than using labels like `[STR]` or `(Athletics)`.
- When an entry has a mechanical summary, state it plainly first and put the joke after it.
- Never put exact numbers in flavor text, because numbers live in the data tables.

## Emotional stakes and sincere moments

**Real emotional stakes are allowed occasionally,** even within comedic scenes. A character's grief, fear, or love can be real and taken seriously, while the humor comes from elsewhere in the scene. Never mock the pain itself. For example, a caged merchant genuinely wants his family back. The comedy can come from what else he cares about, but not from the loss.

**Fully sincere moments are a separate thing.** Sincerity is reserved for truly important moments, and only where explicitly requested, such as the climax of a major storyline or a legendary relic. In these moments the comedy drops away entirely, and the writing becomes earnest, lyrical, and epic. The contrast is what makes them land, so never introduce a sincere moment on your own initiative.

## System text

Tooltips, stat explanations, menus, confirmations, and error messages are **plain and clear first**. Unlike flavor text, they may refer to the game directly ("Pause the game"). A small dose of personality is fine, as in a cancel button that pleads "Don't do it!", but never at the cost of clarity.

## Spelling and punctuation

Use American spelling (color, armor, honor, traveler).

Never use dashes as punctuation in player-facing text: no em dashes (—), en dashes (–) or spaced hyphens ( - ). Readers take them as a sign of machine writing. Use a period, a comma, or a colon instead. Hyphens inside words (well-known, hand-painted) are fine.

## Don'ts

- Don't step outside the world (games, graphics, players, developers, the real world), except in system text.
- Don't use modern references that aren't established in the lore.
- Don't give characters mundane real-world names as a joke. Gambabuchu's given name, Steve, is a deliberate one-off.
- Don't let characters wink at the joke.
- Don't make every sentence a punchline.
- Don't use stock fantasy openers, such as "Ah, adventurer..." or "Greetings, traveler."
- Don't write long exposition outside lore books unless asked.
- Don't trade a choice's clarity for a gag.

## Examples

**Item**
- *Dawnsplitter.* Its smith wept when he finished it, then hid it from his other swords to spare their feelings.

**Dialogue**
- Understated gatekeeper: "The gate is open. It's always open. I'm here to discourage you."
- Hysterical herbalist: "DON'T TOUCH THE MUSHROOMS. Touch anything else. NOT THE MUSHROOMS!"

**Event with options**
- A goblin sits at a desk in the middle of the corridor, stamping forms. A sign reads: TOLL: ONE (1) SHOE. Goblins do not wear shoes. Nobody has ever asked why.
  - Hand over a shoe.
  - Fight the goblin. He has a very large stamp.
  - Sneak past while he's filing.

**Lore book entry**
- The chief of a goblin tribe is chosen by girth. The fatter a goblin, the more he loves to eat, and the more food he will bring home. Shamans add that fatness is a sign of supernatural awesomeness, which is also a good quality in a leader.

## Lore and consistency

Once a joke becomes lore, it is canon. All established lore lives in `docs/design/lore.md`.
- Check it before inventing factions, places, peoples, recurring characters, or professions, and don't contradict it.
- When you add something likely to recur, append it to the lore file and flag it for review.
