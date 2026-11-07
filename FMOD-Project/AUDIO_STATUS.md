# Estado de implementación de audio

Actualizado: 2026-11-05T15:58:18+01:00

Esta es una matriz local de trabajo, no una modificación ni una aprobación de la hoja del equipo. **No está terminado todo el listado.** Las notas del lead pueden cambiar después de esta copia.

Fuente: [SFX List](https://docs.google.com/spreadsheets/d/1bLMjsE_izpUzA-OrHhN7JUcOVmXOJ4R_/export?format=csv&gid=895060348); SHA-256 `1a63e3af7d495ae158e2a8e6b7e2bd8750e4c6cd6e17184fe986fc5eb50718cf`.

## Revisión 2026-11-06

- Excel releído con sus enlaces incrustados: siguen 61 entradas. Cambios respecto a la copia anterior: Falling sound ahora Low y Kill sound ahora Medium, sin cambio de descripción o fuente.
- Gameplay migrado a `AudioCueId` y accesores generados `GameAudioCues`; no hay llamadas de reproducción/suspensión con rutas literales en la lógica SP/MP.
- 89/89 bindings migrados a identidad backend estable conservando las selecciones existentes. En FMOD el GUID es autoritativo; la ruta visible se sincroniza después de Build/Refresh de bancos. Un evento borrado/recreado no se reasigna por nombre.
- 56 comprobaciones dirigidas pasadas, incluyendo renombrado con GUID/count iguales, caches/identidad, símbolos estables, selección personalizada, edición real y preview. Preview real comprobado usando el GUID de Dash con una ruta informativa intencionadamente obsoleta.

## Verificación realizada

- Revisión posterior: catálogo editable y ventana Audio References implementados. Verificadas referencias reales en assets, prefabs base/variant/nested, escenas y subassets Timeline; escritura FMOD GUID+Path, rechazo de índice obsoleto y Undo aislado de cambios anteriores.
- Tests de audio, interfaz y editor ejecutados de forma dirigida; no es una suite completa del proyecto.
- Compilación comprobada y prueba SP con backend por interfaz: motores activos, voces independientes, pausa y limpieza de una ráfaga de 25 one-shots. Sin errores de audio observados en esa prueba.
- Build FMOD terminado con éxito y staging de bancos para Unity. No es una build completa del juego.
- No se ha realizado prueba host+cliente de esta versión; se evitó abrir otro editor con el margen de RAM observado. La mezcla auditiva y sincronía fina requieren revisión.

## Fuentes y arquitectura

- 53 muestras Kenney seleccionadas, CC0; licencias y hashes en `FastAndFracture_FMOD/Assets/KenneyCC0/Sources.json`.
- Otros sonidos son reutilizados del proyecto existente; no se les ha atribuido una licencia nueva ni se ha descargado audio de YouTube.
- Cero gasto y ninguna voz nueva clonada/generada en un servicio de pago. Las descargas automáticas de Pixabay no se pudieron completar.
- 33 cues nuevos, 89 eventos totales. Se conservan los GUID de los eventos originales y el contenido de las músicas de menú/partida.
- Cuatro montajes originales incorporados desde el chip Drive MUSIC de la hoja: Carme `carmen_loss.wav`, Josefino `josefino_death.wav`, María `loss_1.wav` (asignación confirmada por el usuario), Pepe `pepe_loss.wav`. Fuentes y hashes en `FastAndFracture_FMOD/Assets/MusicSounds/CharacterResults/Sources.json`.
- Las cuatro timelines tienen una pista Original MUSIC sin alargar la animación. Carme usa cierre con fade porque el archivo original supera la duración actual del vídeo. La música de menú se suspende por cue durante la cinemática y se restaura al finalizar/salir, sin cambiar preferencias de volumen.
- Contrato portable en `Fast-and-fractured/Assets/Utilities/Audio/Core`; adaptador en `Fmod`. Los puentes antiguos y las timelines aún dependen de FMOD.

## Matriz

`Conectado` significa implementación presente, no validación auditiva o multijugador completa. `Parcial` y `Pendiente` no deben marcarse como Done en la hoja.

| Fila actual | Entrada | Estado local | Implementación / pendiente |
|---:|---|---|---|
| 2 | Main Menu Music | Preexistente preservado | Marcado Done en la hoja; metadata de música original comprobada sin cambios. |
| 3 | Main Game Music | Preexistente preservado | Marcado Done en la hoja; metadata de música original comprobada sin cambios. |
| 4 | Winning Screen Music (SFX) | Parcial | Conservados temas/voz existentes y añadidas capas de efectos. Falta verificar que estén las músicas específicas del diseñador y audicionar todas las victorias. |
| 5 | Losing Screen Music (SFX) | Conectado; QA pendiente | Los cuatro montajes originales de MUSIC están conectados a su personaje. Duraciones de animación conservadas; cierre de Carme con fade. Voces anteriores preservadas y menú sin música superpuesta; pendiente revisión auditiva fina. |
| 6 | Crash Sound (static collider colision) | Conectado; QA pendiente | StaticCollision: 5 muestras impactGeneric_light, pitch/volumen aleatorios, intensidad por velocidad y masa efectiva. |
| 7 | Maria Antonia Special Ability Sound | Conectado; QA pendiente | SuperSaiyanMariaAntonia: ganancia y loop; voz por emisor, se detiene al acabar/desactivar/morir. |
| 8 | Carme Special Ability Sound (chicken sounds) | Conectado; QA pendiente | Lanzamiento con EggShoot más intenso/agudo; cloqueo, pasos y grito en aterrizaje/empuje; limpieza de loops. |
| 9 | Josefino Special Ability Sound | Parcial | ShieldActivate/ShieldEnd por estado confirmado y ShieldImpact en el rebote SP. El reflejo de proyectil MP no tiene hook equivalente verificado. |
| 10 | Pepe Special Ability Sound | Conectado; QA pendiente | TomatoLaunch/TomatoImpact y capa TomatoSplat, conectadas en SP y presentación MP. |
| 11 | Healing sound effect | Conectado; QA pendiente | Heal: cue RPG CC0; recuperación local por cambio de salud; deduplicación con pickup. |
| 12 | Dash sound | Conectado; QA pendiente | Dash: transición observada de dash, no por tick de simulación/replay. |
| 13 | Push shoot sarten | Conectado; QA pendiente | PanThrow metalPot3; BounceMetal para los rebotes. |
| 14 | Explosion | Conectado; QA pendiente | Evento existente conectado a explosión SP y RPC de presentación MP. |
| 15 | Boucing Push Shoot | Conectado; QA pendiente | BounceMetal para golf/sartén; BounceSoft para gallina/melón; RPC autoritativo en MP. |
| 16 | Collision with cars | Conectado; QA pendiente | CarCollision: 5 variantes impactPlate_heavy, variación y una emisión por par de vehículos. |
| 17 | Falling sound | Conectado; QA pendiente | FallingWind suave mientras cae; parada al aterrizar/morir. Falling cartoon solo al morir durante la caída. |
| 18 | Derrape sound | Pendiente | Falta muestra apropiada; recurso Pixabay localizado pero descarga automática bloqueada. No se ha puesto un sustituto que no encaje. |
| 19 | Kill sound | Conectado; QA pendiente | Kill: feedback personal; hook SP CelebrateKill y notificación MP confirmada al atacante local. |
| 20 | Sandstorm appear sound | Conectado; QA pendiente | SandstormWarning por aparición, con guard contra repetición en el mismo emisor. |
| 21 | Bullet impact sound characters | Conectado; QA pendiente | CharacterBulletImpact: 5 impactPlate_light según la última nota, conectado SP/MP. |
| 22 | Bullets impact sound (non characters) | Conectado; QA pendiente | WorldBulletImpact: 5 impactWood_light; sin sonido de impacto al agotar rango en MP. |
| 23 | Breakeable platforms breaking sound | Conectado; QA pendiente | PlatformCrack: 5 impactWood_medium; PlatformSplinter reutiliza rotura de madera existente. Restauración MP silenciosa. |
| 24 | Tramploline sound | Conectado; QA pendiente | Trampoline en la liberación/animación; cooldown de audio contra doble callback host. |
| 25 | Overheat sound | Conectado; QA pendiente | Aviso local + loop OverheatFire thrusterFire_003; fritura adicional de María; señal al terminar y limpieza. |
| 26 | Golf Cart Engine Sound | Conectado; QA pendiente | Ganancia +3 dB, espacialización y motor continuo controlado por voz/CarState. Motor SP observado activo. |
| 27 | Tractor Engine Sound | Conectado; QA pendiente | Ganancia +3 dB, espacialización, pitch/volumen por movimiento. Motor SP observado activo. |
| 28 | Quad Engine Sound | Conectado; QA pendiente | Ganancia +3 dB, espacialización, pitch/volumen por movimiento. Motor SP observado activo. |
| 29 | Electric Mobility Scooter Sound | Parcial | Vespa recibe ajuste de ganancia/3D, pero el prefab de María usa GolfCart: se conserva esa asignación comprobada. Falta una fuente/asignación específica aceptada. |
| 30 | UI Interaction Sounds (buttons, etc.) | Conectado; QA pendiente | Click y hover/selección con pitch aleatorio 0,94–1,06, sin afectar RNG de gameplay; toggles/sliders y click que desactiva pantalla. |
| 31 | Maria Antonia victory animation sounds | Conectado; QA pendiente | Capa de fritura del evento original reducida a -12 dB; música y duración conservadas. |
| 32 | Josefino victory animation sounds | Conectado; QA pendiente | Capa de monedas CC0 añadida al evento, alineada con el tramo de aparición del dinero. Es moneda, no una grabación de caja registradora. |
| 33 | Carme victory animation sounds | Conectado; QA pendiente | Pasos hasta el final del clip de caminar y cloqueos al levantar la gallina; sin cambiar animación ni voz original. |
| 34 | Windmill sound | Conectado; QA pendiente | WindmillSFX existente conectado al molino; instancia adjunta y parada al desactivar. |
| 35 | Sound for health/Cooldown item pick up | Conectado; QA pendiente | Heal/CoolingComplete locales; TargetRpc para quien recoge en MP; deduplicación con recuperación de salud. |
| 36 | Sound for buff up item | Conectado; QA pendiente | BuffPickup separado del de salud/enfriamiento, SP y receptor MP. |
| 37 | Pepe Defeat animation sound | Conectado; QA pendiente | Voz existente conectada a NewPepeDefeat; timeline conserva 18,233 s. Sin voces nuevas generadas. |
| 38 | Maria Antonia defeat animation sound | Pendiente | La nota pide voz culpando al jugador mientras llora: falta interpretación o generación gratuita autorizada adecuada. |
| 39 | Josefino defeat animation sound | Pendiente | Pendiente revisar/cambiar grito y sincronización; no sustituido sin audicionar la animación. |
| 40 | Carme defeat animation sound | Parcial | Pasos existentes más audibles y cloqueos durante persecución; gritos originales conservados. Falta la mejora del grito pedida en la nota. |
| 41 | Morning ambient sound (gallo sound, crickets,birds) | Pendiente | Existen bosque/gallinas, pero no un ambiente completo gallo/grillos/aves ni controlador horario verificado. |
| 42 | Mid day (birds, cars, cigarras) | Pendiente | Faltan las capas naturales específicas y su integración horaria. |
| 43 | Sunset (sheep sounds, crickets) | Pendiente | Faltan ovejas/grillos adecuados con fuente utilizable. |
| 44 | Evening Night (wolf, crickets, owls) | Pendiente | Faltan lobo/grillos/búhos adecuados con fuente utilizable. |
| 45 | Water sound | Pendiente | Falta ambiente apropiado para los cuerpos de agua; no se ha confundido grifo de Kenney con ambiente costero. |
| 46 | Construction sounds | Parcial | Zona local generada a partir de landmarks de obra y evento existente. Falta prueba funcional en ConstructionMap y MP. |
| 47 | Mercadillo sound | Parcial | Zona local a partir de props Mercadillo y evento existente. Falta prueba funcional de sus límites en VillageMap y MP. |
| 48 | Bar sound (futbol, people talking) | Pendiente | Falta ambiente y locución/partido con permiso de uso; no se ha extraído YouTube ni retransmisiones. |
| 49 | Seagulls in josefino map ambience sound | Pendiente | Recurso Pixabay localizado; sin descarga comprobada ni integración. |
| 50 | weird alien sound and fire sound from the ovni. | Pendiente | Hay recursos sci-fi y voz alien existentes, pero faltan selección y disparadores concretos de ovni/fuego verificados. |
| 51 | Sandstorm sound | Conectado; QA pendiente | SandstormLoop local al entrar en el volumen rotado; parada al salir/desactivar o morir. |
| 52 | Tomato Shooting SFX | Conectado; QA pendiente | Evento/GUID original; variación aleatoria de pitch y volumen. |
| 53 | Golf Ball Shooting SFX | Conectado; QA pendiente | Evento/GUID original; variación aleatoria de pitch y volumen. |
| 54 | Egg Shooting SFX | Conectado; QA pendiente | Evento/GUID original; variación aleatoria de pitch y volumen. |
| 55 | Croquette Shooting SFX | Conectado; QA pendiente | Eventos/GUID originales; variación de pitch y volumen; sartén usa cue propio. |
| 56 | Pepe Victory animation sounds | Parcial | Crujidos CC0 en el tramo de movimiento del tractor e impacto plate_heavy en el instante de la explosión ya existente. Falta decidir/validar el efecto final de caída de la nota. |
| 57 | Josefino getting hit quotes | Parcial | JosefinoHurt reutiliza ouch.wav al bajar salud, con cooldown. Es reacción no verbal, no frases nuevas. |
| 58 | Maria Antonia getting hit quotes | Pendiente | Faltan frases/interpretación adecuadas y autorizadas; no se han clonado voces. |
| 59 | Carme getting hit quotes | Pendiente | Faltan frases/interpretación adecuadas y autorizadas; no se han clonado voces. |
| 60 | Pepe getting hit quotes | Pendiente | Faltan frases/interpretación adecuadas y autorizadas; no se han clonado voces. |
| 61 | Lava sound | Pendiente | Falta ambiente de lava adecuado y validación con el crecimiento; no se ha extraído la referencia YouTube. |
| 62 | Casa que absorve (wind sound) | Pendiente | Faltan aspiradora/expulsión y sus hooks concretos. No se han usado referencias sin permiso. |

## Balance

- Preexistente preservado: **2**.
- Parcial: **8**.
- Conectado; QA pendiente: **35**.
- Pendiente: **16**.

Adicional a la lista vigente: se mantiene un cue de crujido de puente de una versión anterior de la hoja; no se cuenta como cobertura de una entrada actual. Sin commits ni push.
