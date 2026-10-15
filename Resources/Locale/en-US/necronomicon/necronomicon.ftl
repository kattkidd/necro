## Necronomicon antagonist (Evil Dead). Placeholder text; keep it short.

necronomicon-title = Necronomicon
necronomicon-description = An old recording began playing somewhere aboard as the shift started. Something heard it. Find the book and burn it.

# Roles
roles-antag-necronomicon-name = Kandarian Demon
roles-antag-necronomicon-objective = Possess the crew through the Necronomicon until every soul aboard is consumed. Keep the book from burning.
roles-antag-deadite-name = Deadite
roles-antag-deadite-objective = Serve the Necronomicon. Swallow their souls.
roles-antag-demonspawn-name = Demonspawn
roles-antag-demonspawn-objective = Serve the Necronomicon. Tear the living apart.
role-subtype-kandarian-demon = Kandarian Demon
role-subtype-deadite = Deadite
role-subtype-demonspawn = Demonspawn

necro-briefing = You are a Kandarian Demon bound to the Necronomicon. An old recording of the reading played as the shift began, and you are awake. The book waits in the ether: manifest it where you choose, whisper JOIN US, possess the crew and consume every soul. A living person holding the book stops it leaving the world, and if they burn it, you are banished.
necro-briefing-character = You serve the Necronomicon. Spend Soul Power (earned from Fear) in your Skill Store. The book cannot leave the physical world while a living, unpossessed person carries it, and fire destroys it.
deadite-briefing = You are a Deadite, possessed by a Kandarian Demon. Stay disguised as long as you like, then reveal your true form. Bite the living to prepare them for possession. Speak Ancient Sumerian with :k. Once revealed, your laughs, screams and snarls come out in the voice of the dead. Beware the chaplain: if you are restrained, a holy rite can cast the demon out of you.
demonspawn-briefing = You are Demonspawn, summoned by the Necronomicon. Claw, bite and terrify. Speak Ancient Sumerian with :k. In time you will grow into something bigger. Holy water and Bibles will melt you to dust on contact.

ghost-role-information-demonspawn-name = Demonspawn
ghost-role-information-demonspawn-description = A small, vicious thing summoned by the Necronomicon. Serve the book.
ghost-role-information-demonspawn-adult-name = Grown Demonspawn
ghost-role-information-demonspawn-adult-description = A tall, grey, black-eyed thing that was once a summoned imp. Serve the book.
ghost-role-information-demonspawn-rules = You are a [color={role-type-team-antagonist-color}][bold]{role-type-team-antagonist-name}[/bold][/color] serving the Necronomicon and its Deadites. Attack the living crew.

chat-radio-sumerian = Ancient Sumerian

name-format-kandarian = {$part0}

# Store
necro-store-title = Necronomicon
store-currency-display-soul-power = Soul Power
store-category-necro-appearance = Appearance
store-category-necro-possession = Possession
store-category-necro-fear = Fear
store-category-necro-powers = Supernatural Powers
store-category-necro-manifestation = Physical Manifestation
store-category-necro-demonspawn = Demonspawn
store-category-necro-infiltration = Infiltration
store-category-necro-deadite-tree = Deadite Evolution

necro-catalog-form-desc = Choose the shape you will wear. Each form has its own speed, toughness and claws once you buy a Physical Form. Can be changed at any time.
necro-catalog-form-abomination-name = Form: Abomination
necro-catalog-form-hellspawn-name = Form: Hellspawn
necro-catalog-form-flesh-name = Form: Flesh Golem
necro-catalog-form-wraith-name = Form: Wraith
necro-catalog-form-lover-name = Form: Shambling Lover
necro-catalog-form-crawler-name = Form: Crawler
necro-catalog-form-apparition-name = Form: Apparition
necro-catalog-charge-name = Possession Charge
necro-catalog-charge-desc = One more soul for the book. Each successful possession consumes a charge.
necro-catalog-fear-amplify-name = Amplify Fear
necro-catalog-fear-amplify-desc = All Fear yields 50% more Soul Power. Stacks twice.
necro-catalog-dread-aura-name = Dread Aura
necro-catalog-dread-aura-desc = Revealed Deadites, Demonspawn and your physical form generate double Fear just by being near the living.
necro-catalog-whisper-name = Whisper
necro-catalog-whisper-desc = Unlocks Whisper: a private terror for one crew member within 12 tiles. 2 SP per use. Mindshields dull it.
necro-catalog-dark-presence-name = Dark Presence
necro-catalog-dark-presence-desc = Unlocks Dark Presence: lights flicker and machines misbehave around you. 4 SP per use.
necro-catalog-physical-form-name = Physical Form
necro-catalog-physical-form-desc = Unlocks taking a corporeal, combat-capable body in your chosen form (150 health, 14 slash). 5 SP per use. If the body dies you return to the ether and must wait 2 minutes.
necro-catalog-hardened-flesh-name = Hardened Flesh
necro-catalog-hardened-flesh-desc = Your physical form gains 75 health. Stacks twice. Requires Physical Form.
necro-catalog-rending-claws-name = Rending Claws
necro-catalog-rending-claws-desc = Your physical form deals 6 more slash damage. Stacks twice. Requires Physical Form.
necro-catalog-demonspawn-name = Summon Demonspawn
necro-catalog-demonspawn-desc = A vicious grey imp crawls out at the book (or at you). Taken over by a ghost. After ten minutes it grows into a tall, black-eyed adult. Holy water or a Bible destroys it.
necro-catalog-swift-possession-name = Swift Possession
necro-catalog-swift-possession-desc = Possession takes 3 seconds instead of 6.
necro-catalog-far-whisper-name = Far Whisper
necro-catalog-far-whisper-desc = JOIN US reaches 18 tiles from you instead of 12.

# Demon / book
necro-not-enough-sp = You need {$cost} Soul Power (you have {$current}).
necro-dormant = The reading has not finished yet. You are not fully awake.
necro-demon-examine = Soul Power: [color=red]{$sp}[/color]. Possession charges: [color=red]{$charges}[/color]. {$awake ->
    [1] You are awake.
   *[0] You are dormant.
}
necro-too-far = They are too far away.
necro-book-transition-busy = The book is already between worlds.
necro-book-already-manifested = The book is already in the physical world.
necro-book-already-hidden = The book is already in the ether.
necro-book-not-while-physical = You cannot summon the book while wearing a body.
necro-book-pinned = Living hands hold the book. It will not leave.
necro-book-pinned-interrupted = Living hands seize the book! It is dragged back into the world.
necro-book-settles = The book shudders, then goes still.
necro-book-manifest-start = You call the book into the world...
necro-book-demanifest-start = You begin to draw the book back into the ether...
necro-book-stirring = The book's pages flutter on their own.
necro-book-appears = A book bound in skin falls out of thin air.
necro-book-manifested = The Necronomicon is in the physical world.
necro-book-vanishes = The book fades away like smoke.
necro-book-demanifested = The Necronomicon returns to the ether.
necro-book-must-be-manifested = The Necronomicon must be in the physical world.
necro-book-too-far = The book is too far from them.
necro-book-examine-burning = [color=orange]It is charred ({$percent}% burnt).[/color]
necro-book-burn-stage-1 = The pages blacken and a voice screams from inside the book!
necro-book-burn-stage-2 = The face on the cover twists in agony!
necro-book-burn-stage-3 = The book is almost gone!
necro-book-burning-demon = THE BOOK IS BURNING!
necro-book-destroyed = The Necronomicon crumbles to ash with a terrible howl.
necro-banished = The book is ash. You are torn from this world.

necro-recording-start = A crackling voice begins reading in an ancient tongue...
necro-recording-finished = The recording ends. Something outside the walls answers.
necro-recording-spent = The tape only hisses now.
necro-recording-examine = The label reads: "Translations of the Book of the Dead".
necro-recording-examine-spent = The reel has run out. Someone, or something, already played it.
necro-awakened = The recording has finished the reading. You are AWAKE. The Necronomicon waits in the ether: use Manifest Necronomicon to bring it into the world wherever you stand.

# Possession
necro-target-invalid = That is not a soul you can take.
necro-target-already = That one already belongs to the book.
necro-target-blessed = {CAPITALIZE(THE($target))} was freed by a holy rite. The book cannot touch them yet.
necro-target-mindshielded = A mindshield protects {$target}. A revealed Deadite must read the passage to melt it.
necro-target-not-prepared = They are not ready. Whisper JOIN US or have a Deadite bite them first.
necro-no-charges = You have no possession charges. Buy more in the Skill Store.
necro-join-us-victim = Join us...
necro-join-us-shudder = {CAPITALIZE(THE($target))} shudders violently.
necro-join-us-success = {CAPITALIZE(THE($target))} heard the whisper and is ready for possession.
necro-possess-start-victim = Something is crawling into your mind!
necro-possess-success = {CAPITALIZE(THE($target))} is yours. Charges left: {$charges}.

# Powers
necro-whisper-sent = You whisper to {THE($target)}.
necro-whisper-line-1 = We're going to get you...
necro-whisper-line-2 = Dead by dawn. Dead by dawn.
necro-whisper-line-3 = Something is right behind you.
necro-whisper-line-4 = Your friends are already ours.
necro-whisper-line-5 = Join us...
necro-whisper-line-6 = We will swallow your soul.
necro-form-selected = Your form shifts.
necro-charge-bought = Possession charges: {$charges}.
necro-demonspawn-summoned = Something small and grey claws its way out of nothing!
necro-upgrade-bought = Your power grows.
necro-form-recovering = Your last body was destroyed. You can manifest again in {$seconds} seconds.
necro-form-manifest = {CAPITALIZE($demon)} tears itself into existence!
necro-form-destroyed = Your body is destroyed! You slip back into the ether.
necro-form-dissolves = The creature collapses into black smoke.

# Deadites
deadite-name-format = Deadite '{$baseName}'
deadite-converted = Your soul belongs to the Necronomicon now. You are a Deadite. Act normal... until you don't.
deadite-reveal = {CAPITALIZE(THE($target))}'s skin turns corpse-pale and their eyes go milky white!
deadite-rises = {CAPITALIZE(THE($target))} jerks back to their feet!
deadite-bite = {CAPITALIZE(THE($user))} sinks their teeth into {THE($target)}!
deadite-bite-shielded = The mindshield repels the curse.
deadite-bite-prepared = A cold voice whispers: join us...
deadite-ritual-start = {CAPITALIZE(THE($user))} begins reading aloud from the book at {THE($target)}!
deadite-ritual-not-revealed = You must reveal your true form to read from the book.
deadite-ritual-not-shielded = {CAPITALIZE(THE($target))} has no mindshield to melt.
deadite-ritual-book-far = The manifested book must be within reach of you and your victim.
deadite-ritual-melt = {CAPITALIZE(THE($target))}'s mindshield implant smokes and melts away!
deadite-ritual-melt-victim = Your mindshield burns away. Nothing protects your mind now.
deadite-severed = {CAPITALIZE(THE($target))}'s head is severed, but it keeps screaming!
deadite-released = {CAPITALIZE(THE($target))} collapses as the curse lifts.
demonspawn-banished = The demonspawn shrieks and dissolves.

# Round
necro-announcement-sender = Central Command
necro-crew-victory-announcement = The Necronomicon has been destroyed. Deadite activity has ceased. Recover the bodies and report for evacuation.
necro-victory-announcement = ...no living soul answers. Join us.
necro-round-end-demon = The Kandarian Demon was [color=red]{$name}[/color] ([color=gray]{$username}[/color]).
necro-round-end-crew-victory = [color=green]The crew burned the Necronomicon![/color]
necro-round-end-necro-victory = [color=red]Every soul aboard was swallowed by the Necronomicon.[/color]
necro-round-end-ongoing = The Necronomicon was never destroyed.

# Accent (revealed Deadites; all-caps is applied separately)
accent-deadite-1 = Join us!
accent-deadite-2 = We're gonna get you!
accent-deadite-3 = Dead by dawn!
accent-deadite-4 = I'll swallow your soul!
accent-deadite-5 = Why don't you come down here, you little creep!
accent-deadite-6 = Your soul is ours!
accent-deadite-7 = You'll be one of us!
accent-deadite-8 = Get over here, meat!

# Demon names (short and guttural). Add more and bump count in Datasets/Names/kandarian.yml.
names-kandarian-first-dataset-1 = Illigos
names-kandarian-first-dataset-2 = Bhal
names-kandarian-first-dataset-3 = Kesh
names-kandarian-first-dataset-4 = Vorr
names-kandarian-first-dataset-5 = Ghul
names-kandarian-first-dataset-6 = Azzur
names-kandarian-first-dataset-7 = Thrak
names-kandarian-first-dataset-8 = Moloth
names-kandarian-first-dataset-9 = Zerat
names-kandarian-first-dataset-10 = Ulm
names-kandarian-first-dataset-11 = Nezz
names-kandarian-first-dataset-12 = Khar
names-kandarian-first-dataset-13 = Ozul
names-kandarian-first-dataset-14 = Varn
names-kandarian-first-dataset-15 = Drael
names-kandarian-first-dataset-16 = Sabbu
names-kandarian-first-dataset-17 = Gorr
names-kandarian-first-dataset-18 = Ishk
names-kandarian-first-dataset-19 = Belos
names-kandarian-first-dataset-20 = Xul
names-kandarian-first-dataset-21 = Morgh
names-kandarian-first-dataset-22 = Tazz
names-kandarian-first-dataset-23 = Uzrah
names-kandarian-first-dataset-24 = Nergal
names-kandarian-first-dataset-25 = Kaz
names-kandarian-first-dataset-26 = Orrus
names-kandarian-first-dataset-27 = Vesh
names-kandarian-first-dataset-28 = Ghath
names-kandarian-first-dataset-29 = Lurm
names-kandarian-first-dataset-30 = Zoth

# Demonic possession (harmless puppeting)
necro-catalog-puppet-name = Demonic Possession
necro-catalog-puppet-desc = Unlocks seizing anyone within 12 tiles for 45 seconds, mindshielded or not. They collapse and can't act or speak for themselves; everything you say comes out of their mouth. Harmless and temporary. 3 SP per use; target your host again to let go early. (Ancient Sumerian is unavailable while you hold a host.)
necro-catalog-puppet-announce-name = Speak to the Station
necro-catalog-puppet-announce-desc = While possessing someone, make your next sentence a station announcement signed with their name. 3 SP per announcement, 2 minute cooldown. Requires Demonic Possession.
necro-puppet-busy = You are already wearing someone.
necro-puppet-not-possessing = You must be possessing someone first.
necro-puppet-start-others = {CAPITALIZE(THE($target))} convulses and collapses, eyes rolling back!
necro-puppet-start-host = Something cold slides into you. Your body won't answer. Your mouth moves on its own...
necro-puppet-start-demon = You are inside {THE($target)} for {$seconds} seconds. Everything you say comes out of their mouth.
necro-puppet-end-host = The presence leaves you. You can move again.
necro-puppet-end-demon = You slip out of your host.
necro-puppet-announce-armed = Your next sentence will be broadcast to the whole station in your host's name.
necro-puppet-announce-disarmed = Announcement cancelled.
necro-puppet-announce-cooldown = The station is still listening for you... wait {$seconds} seconds.
necro-puppet-cooldown = You need to gather yourself... wait {$seconds} seconds.

# HUD
alerts-necro-sp-name = Soul Power
alerts-necro-sp-desc = Earned from the crew's fear. Spend it in the Skill Store and on your powers.
alerts-necro-charges-name = Possession Charges
alerts-necro-charges-desc = Each possession uses one. Buy more in the Skill Store.
alerts-necro-book-name = The Necronomicon
alerts-necro-book-desc = Where your book is: in the ether, in the world, held by the living (it cannot leave), or burning.
necro-status-book-held = Living hands hold your book: {CAPITALIZE(THE($holder))}! It cannot leave the world.
necro-status-book-released = The living let go of your book.
necro-status-book-burning = YOUR BOOK IS ON FIRE!
necro-status-book-extinguished = The flames on your book die out.

# Examine
necro-book-examine-hint = [color=orange]Something about this book wants to be put down. Hold onto it: it can't vanish from your hands. Fire might end it for good.[/color]

# Round end
necro-round-end-header = [color=darkred][bold]The Necronomicon[/bold][/color]
necro-round-end-deadite = - [color=red]{$name}[/color] became a Deadite
necro-round-end-deadite-exorcised = - [color=red]{$name}[/color] became a Deadite, but was [color=green]exorcised[/color]
necro-round-end-survivors = { $count ->
    [0] No living soul remained aboard.
    [one] 1 living soul remained aboard.
   *[other] {$count} living souls remained aboard.
}

# Guidebook
guide-entry-necronomicon = Necronomicon

# Appearance loadout
loadout-group-necro-appearance = Appearance
loadout-group-necro-aura = Aura

# Round-end statistics
necro-round-end-time-survived = The demon walked the station for [color=red]{$minutes}m {$seconds}s[/color] and was never banished.
necro-round-end-time-banished = The demon lasted [color=red]{$minutes}m {$seconds}s[/color] before the book burned.
necro-round-end-stat-souls = Souls consumed: [color=red]{$count}[/color]
necro-round-end-stat-deadites = Deadites created: [color=red]{$count}[/color] ({$exorcised} exorcised)
necro-round-end-stat-possessions = Successful possessions: [color=red]{$count}[/color] ({$deadite} into Deadites, {$puppet} demonic possessions)
necro-round-end-stat-demonspawn = Demonspawn summoned: [color=red]{$count}[/color] ({$matured} matured)
necro-round-end-stat-fear = Fear harvested: [color=red]{$fear}[/color] SP. Total Soul Power earned: [color=red]{$sp}[/color]
necro-round-end-stat-rituals = Mindshields melted: [color=red]{$rituals}[/color]. Exorcisms: [color=green]{$exorcisms}[/color] of {$attempts} attempted

## Chaplain exorcism

deadite-exorcise-verb = Exorcise
deadite-exorcise-verb-message = Perform a rite of exorcism. They must be restrained, buckled down or incapacitated.
deadite-exorcise-cooldown = You need to gather your strength before another rite. ({$seconds}s)
deadite-exorcise-already = A rite is already being performed over them.
deadite-exorcise-not-restrained = {CAPITALIZE(THE($target))} must be restrained, buckled down or incapacitated for the rite.
deadite-exorcise-start = {CAPITALIZE(THE($user))} raises a holy book over {THE($target)} and begins an exorcism!
deadite-exorcise-start-self = You begin the rite of exorcism over {THE($target)}. Hold still for {$seconds} seconds.
deadite-exorcise-start-victim = Holy words wash over you.
deadite-exorcise-writhe = {CAPITALIZE(THE($target))} thrashes and howls as the holy words strike!
deadite-exorcise-burns = THE HOLY WORDS BURN! The priest is trying to cast you out!
deadite-exorcise-demon-alert = A priest is casting you out of {$target}!
deadite-exorcise-interrupted = The rite is broken.
deadite-exorcise-interrupted-deadite = The burning stops. You are still the book's.
deadite-exorcise-puppet = The voice speaking through {THE($target)} is driven out!
deadite-exorcise-nothing = The rite ends. There is no evil in {THE($target)}.
deadite-exorcise-success = With a final shriek, the darkness tears itself out of {THE($target)}!
deadite-exorcise-success-self = The demon is cast out of {THE($target)}!
deadite-exorcise-success-victim = The cold thing inside you is gone. You are yourself again.
deadite-exorcise-demon-lost = Your hold on {$target} is broken!
deadite-blessing-faded = The warmth of the blessing fades.

## Deadite evolution tree

necro-catalog-infiltrator-1-name = Infiltrator I: Borrowed Skin
necro-catalog-infiltrator-1-desc = Your disguised Deadites bite silently: no sound, and only the victim notices. Counts as one of your two evolution paths.
necro-catalog-infiltrator-2-name = Infiltrator II: Deep Roots
necro-catalog-infiltrator-2-desc = Exorcisms take 50% longer, and revealed Deadites can pull their host's face back on (Wear Their Face).
necro-catalog-brute-1-name = Brute I: Unholy Strength
necro-catalog-brute-1-desc = Revealed Deadites hit 60% harder and rise from critical 40% sooner. Counts as one of your two evolution paths.
necro-catalog-brute-2-name = Brute II: Unyielding
necro-catalog-brute-2-desc = Revealed Deadites take 25% less damage, and it takes a much heavier blow to sever their heads.
necro-catalog-stalker-1-name = Stalker I: Hunter's Gait
necro-catalog-stalker-1-desc = Revealed Deadites move faster, and every Deadite's bite reaches further. Counts as one of your two evolution paths.
necro-catalog-stalker-2-name = Stalker II: Hamstring
necro-catalog-stalker-2-desc = A Deadite's bite badly slows its victim for a few seconds. Nobody outruns you.
necro-catalog-screamer-1-name = Screamer I: Unholy Shriek
necro-catalog-screamer-1-desc = Deadites gain a shriek that terrifies everyone nearby and harvests their fear. Counts as one of your two evolution paths.
necro-catalog-screamer-2-name = Screamer II: Deafening Wail
necro-catalog-screamer-2-desc = The shriek throws nearby crew without a mindshield off their feet.

deadite-tree-gained-infiltratori = The book teaches you to bite without a sound.
deadite-tree-gained-infiltratorii = The book teaches you to wear their face again, and to cling on against holy words.
deadite-tree-gained-brutei = Unholy strength floods your limbs.
deadite-tree-gained-bruteii = Your flesh hardens. Let them try to cut you down.
deadite-tree-gained-stalkeri = Your legs ache to run. Your jaws reach further.
deadite-tree-gained-stalkerii = Your bite will cripple them.
deadite-tree-gained-screameri = A scream builds in your throat. Use it.
deadite-tree-gained-screamerii = Your scream could knock a grown man down.

deadite-bite-silent-victim = Something nips you. Probably nothing.
deadite-bite-silent-user = You quietly bite {THE($target)}. Nobody noticed.
deadite-scream = {CAPITALIZE(THE($user))} lets out an unholy shriek!
deadite-scream-victim = The shriek claws at the inside of your skull!
deadite-scream-not-revealed = You must reveal your true form to shriek.
deadite-conceal = {CAPITALIZE(THE($target))}'s face twitches and colour floods back into their skin.
deadite-conceal-self = You pull their face back on. You look like them again.
deadite-conceal-fail = You can't do that right now.

## Demonspawn growth and holy weakness

demonspawn-maturing-soon = Your skin itches and splits. You are about to grow.
demonspawn-matured = {CAPITALIZE(THE($target))} unfolds out of the little body, tall, grey and black-eyed!
demonspawn-matured-self = You have grown. You are bigger, stronger and smarter now. Holy things will still destroy you.
demonspawn-matured-demon = One of your Demonspawn has grown up.
demonspawn-melting = {CAPITALIZE(THE($target))} shrieks as holy power burns through it!
demonspawn-melting-self = HOLY FIRE! You are coming apart!
demonspawn-melting-user = It's melting!
demonspawn-dust = {CAPITALIZE(THE($target))} crumbles into a pile of grey dust.
demonspawn-dust-demon = One of your Demonspawn was destroyed by something holy.

## Kandarian chant and supernatural events

necro-chant-1 = Kanda...
necro-chant-2 = Estrada...
necro-chant-3 = Amantos...
necro-chant-4 = Kanda! Kanda!
necro-chant-5 = Naturom Demonto...
necro-chant-6 = KANDAR!
necro-roar = {CAPITALIZE($demon)} lets out a roar that shakes the walls!
necro-poltergeist = Everything around {THE($target)} rises into the air!
