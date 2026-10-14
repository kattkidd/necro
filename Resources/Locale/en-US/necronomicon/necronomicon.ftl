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

necro-briefing = You are a Kandarian Demon bound to the Necronomicon. An old recording of the reading played as the shift began, and you are awake. The book fell out of the air beside the recorder. Hide it, whisper JOIN US, possess the crew and consume every soul. If they burn the book, you are banished.
necro-briefing-character = You serve the Necronomicon. Spend Soul Power (earned from Fear) in your Skill Store. The book cannot leave the physical world while a living, unpossessed person carries it, and fire destroys it.
deadite-briefing = You are a Deadite, possessed by a Kandarian Demon. Stay disguised as long as you like, then reveal your true form. Bite the living to prepare them for possession. Speak Ancient Sumerian with :k.
demonspawn-briefing = You are Demonspawn, summoned by the Necronomicon. Claw, bite and terrify. Speak Ancient Sumerian with :k.

ghost-role-information-demonspawn-name = Demonspawn
ghost-role-information-demonspawn-description = A small, vicious thing summoned by the Necronomicon. Serve the book.
ghost-role-information-demonspawn-rules = You are a [color={role-type-team-antagonist-color}][bold]{role-type-team-antagonist-name}[/bold][/color] serving the Necronomicon and its Deadites. Attack the living crew.

chat-radio-sumerian = Ancient Sumerian

name-format-kandarian = {$part0}, {$part1}

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

necro-catalog-form-desc = Choose the shape you will wear. Cosmetic until you buy a Physical Form; it grants none of the creature's abilities. Can be changed at any time.
necro-catalog-form-abomination-name = Form: Abomination
necro-catalog-form-hellspawn-name = Form: Hellspawn
necro-catalog-form-flesh-name = Form: Flesh Golem
necro-catalog-charge-name = Possession Charge
necro-catalog-charge-desc = One more soul for the book. Each successful possession consumes a charge.
necro-catalog-fear-amplify-name = Amplify Fear
necro-catalog-fear-amplify-desc = All Fear yields 50% more Soul Power. Stacks twice.
necro-catalog-dread-aura-name = Dread Aura
necro-catalog-dread-aura-desc = Revealed Deadites, Demonspawn and your physical form generate double Fear just by being near the living.
necro-catalog-whisper-name = Whisper
necro-catalog-whisper-desc = Unlocks Whisper: a private terror for one crew member within 8 tiles. 2 SP per use. Mindshields dull it.
necro-catalog-dark-presence-name = Dark Presence
necro-catalog-dark-presence-desc = Unlocks Dark Presence: lights flicker and machines misbehave around you. 4 SP per use.
necro-catalog-physical-form-name = Physical Form
necro-catalog-physical-form-desc = Unlocks taking a corporeal, combat-capable body in your chosen form (150 health, 14 slash). 5 SP per use. If the body dies you return to the ether and must wait 2 minutes.
necro-catalog-hardened-flesh-name = Hardened Flesh
necro-catalog-hardened-flesh-desc = Your physical form gains 75 health. Stacks twice. Requires Physical Form.
necro-catalog-rending-claws-name = Rending Claws
necro-catalog-rending-claws-desc = Your physical form deals 6 more slash damage. Stacks twice. Requires Physical Form.
necro-catalog-demonspawn-name = Summon Demonspawn
necro-catalog-demonspawn-desc = A vicious grey imp crawls out at the book (or at you). Taken over by a ghost.
necro-catalog-swift-possession-name = Swift Possession
necro-catalog-swift-possession-desc = Possession takes 3 seconds instead of 6.
necro-catalog-far-whisper-name = Far Whisper
necro-catalog-far-whisper-desc = JOIN US reaches 12 tiles from the book instead of 8.

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
necro-awakened = The recording has finished the reading. You are AWAKE. The book has taken shape beside the recorder.

# Possession
necro-target-invalid = That is not a soul you can take.
necro-target-already = That one already belongs to the book.
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
necro-form-manifest = A monstrous shape tears itself into existence!
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
necro-round-end-conversions = The book claimed [color=red]{$count}[/color] { $count ->
    [one] soul
   *[other] souls
}.
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

# Demon names
names-kandarian-first-dataset-1 = Kharnak
names-kandarian-first-dataset-2 = Ashurath
names-kandarian-first-dataset-3 = Nergul
names-kandarian-first-dataset-4 = Kandar
names-kandarian-first-dataset-5 = Ezreth
names-kandarian-first-dataset-6 = Ulgoth
names-kandarian-first-dataset-7 = Sumuqan
names-kandarian-first-dataset-8 = Ninkharsa
names-kandarian-first-dataset-9 = Belzhaal
names-kandarian-first-dataset-10 = Ereshkul
names-kandarian-first-dataset-11 = Gallu
names-kandarian-first-dataset-12 = Mortuun
names-kandarian-title-dataset-1 = Swallower of Souls
names-kandarian-title-dataset-2 = Who Waits in the Woods
names-kandarian-title-dataset-3 = Voice of the Book
names-kandarian-title-dataset-4 = Dead by Dawn
names-kandarian-title-dataset-5 = the Unburied
names-kandarian-title-dataset-6 = Keeper of the Cellar
names-kandarian-title-dataset-7 = the Whispering Rot
names-kandarian-title-dataset-8 = Child of the Naturom
names-kandarian-title-dataset-9 = the Shape in the Fog
names-kandarian-title-dataset-10 = Who Wears the Dead
