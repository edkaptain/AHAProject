# Corte con mouse sobre una malla 3D de referencia

Adaptación de la idea de `PlanarCutMesh`, `FreeCutMembrane`, `MouseScalpelSphere` y `CorteLibreHud` a superficies trianguladas en 3D.

**Usa un modelo que ya existe en tu escena.** No genera una cuadrícula. Al entrar a Play, crea una copia de su malla en memoria, subdivide los triángulos atravesados por el mouse y abre los bordes de la incisión. El asset original permanece intacto.

## Instalación rápida

1. Copia la carpeta `Assets/CirugiaMalla3D` de este paquete dentro de `Assets` de tu proyecto. Conserva los nombres de los archivos.
2. Espera a que Unity compile. Los scripts tienen nombres y namespace distintos de los anteriores; no los reemplazan.
3. Crea una **Sphere** desde `GameObject > 3D Object > Sphere`, o selecciona tu modelo. Añade el componente **Referenced Mesh Surgery**. Los otros scripts de corte no deben controlar ese mismo objeto.
4. En **Target**, arrastra el objeto que contiene el **MeshFilter** que deseas cortar. Si el componente está en ese mismo objeto, puedes dejar Target vacío. En un FBX suele ser un objeto hijo, no la raíz.
5. Asigna **Input Camera** a la cámara de la escena, orientada hacia el modelo. También puede quedar vacía si tu cámara tiene el tag `MainCamera`.
6. Asigna al modelo un material válido para tu proyecto. Para las paredes internas, puedes asignar otro material rojo a **Wound Material**. Si queda vacío, se clona el primer material del modelo y se intenta teñir de rojo mediante `_BaseColor` o `_Color`.
7. Entra a **Play**, abre **Game**, mantén clic izquierdo y arrastra sobre el modelo, fuera del panel. Suelta para terminar. Usa **Restaurar malla** para borrar las incisiones.

No necesitas poner `SurfaceCutGeometry` en un GameObject: es la clase auxiliar que utiliza el componente.

### Si usas un FBX u otro modelo importado

Selecciona el asset del modelo en Project y activa **Read/Write** en sus opciones de importación. Pulsa **Apply**. Sin acceso de lectura/escritura, el componente mostrará el motivo y se desactivará.

El objeto necesita **MeshFilter + MeshRenderer**. Si tiene varios objetos hijos con mallas, asigna específicamente el que represente la piel o superficie que cortarás. El componente controla un MeshFilter por vez.

### Esfera que representa el bisturí (opcional)

Crea otra esfera pequeña, por ejemplo con escala `(0.02, 0.02, 0.02)`, y arrástrala a **Scalpel Visual**. El script mueve su origen a la posición del mouse sobre la superficie, con un pequeño offset. Solo se mueve mientras mantienes clic. No hay suavizado ni interpolación del movimiento visual.

No necesita SphereCollider ni Rigidbody. No asignes el mismo modelo que estás cortando como Scalpel Visual. Para un bisturí con un modelo complejo, usa un objeto vacío situado en su punta como referencia visual y cuelga el modelo de ese objeto.

## Ajustes del Inspector

Valores iniciales pensados para una esfera de Unity de aproximadamente una unidad de diámetro.

| Campo | Valor inicial | Función |
| --- | ---: | --- |
| Opening Width | 0.012 | Separación solicitada entre labios, en unidades locales. |
| Incision Depth | 0.025 | Profundidad visual de las paredes internas, en unidades locales. |
| Cursor Surface Offset | 0.003 | Separación del origen del bisturí respecto a la superficie, en unidades de mundo. |
| Minimum Stroke Pixels | 3 | Distancia mínima de mouse antes de procesar otro tramo. |
| Maximum Stroke Pixels | 250 | Un movimiento mayor se trata como salto y no se conecta. |
| Maximum Segments Per Frame | 64 | Máximo de tramos visibles sobre caras originales en una operación. |
| Max Surface Triangles | 30000 | Límite de subdivisiones de superficie; nunca elimina caras originales para cumplirlo. |
| Weld Seams | Activado | Une posiciones coincidentes para que el corte cruce costuras UV. |
| Tolerance Relative | 0.000001 | Tolerancia geométrica relativa a la diagonal local del modelo. |
| Show Hud | Activado | Panel con botón para restaurar y mensajes de estado. |
| Show Wire Gizmos | Desactivado | Aristas en Scene/Game con Gizmos activo y el componente seleccionado. |

La apertura se limita localmente para no invertir triángulos pequeños. Por eso aumentar Opening Width no garantiza que toda la incisión tenga ese ancho. Si los bordes se ven muy cerrados, prueba con trazos menos densos, por ejemplo Minimum Stroke Pixels = 5 o 6. No hace falta densificar toda la malla de entrada.

Para un modelo grande o muy pequeño, ajusta Opening Width e Incision Depth según sus dimensiones **locales**. La escala del Transform también afecta su tamaño visual. Las escalas deben ser positivas y distintas de cero.

## Qué cambia respecto a tus scripts

| Original | Esta versión |
| --- | --- |
| Crea una membrana rectangular XZ. | Lee el MeshFilter de un objeto existente. |
| Proyecta el mouse sobre un plano. | Proyecta el trazo sobre las caras visibles de la superficie 3D. |
| Coordenadas materiales 2D. | Posiciones materiales 3D y seguimiento del triángulo original. |
| UV generadas para un rectángulo. | Interpola UV0–UV7, colores, normales y tangentes del modelo. |
| Un material de superficie. | Conserva la asignación por submesh y añade un material de interior. |
| Contacto con una esfera sobre la membrana abierta. | Rayos de cámara sobre la superficie de referencia, con bisturí visual opcional. |

## Cómo se realiza el corte

1. Se clona la malla al iniciar Play y se recuerda su geometría original.
2. Cada desplazamiento del mouse se divide donde cruza la proyección de los triángulos.
3. Para cada intervalo se elige la cara frontal más cercana del modelo. En una esfera, el trazo no se aplica también al lado posterior.
4. Se insertan vértices en los extremos y en las aristas que atraviesa el trazo. Cuando una arista es compartida, se subdividen sus dos caras.
5. Se registra la incisión y se separan los grupos de esquinas a cada lado. Las puntas interiores permanecen unidas. No se borran triángulos completos para fingir el corte.
6. Se añaden paredes interiores en V para dar profundidad visual.

Cada lote comprueba área, orientación y continuidad de la triangulación material. Ante un caso inválido o un límite de triángulos, el lote se revierte y aparece un mensaje en el panel.

## Alcance del prototipo

- Funciona con superficies estáticas trianguladas, abiertas o cerradas, con orientación coherente y hasta dos caras por arista. Puede haber superficies curvas: no se aplana el modelo entero.
- **No equivale a soporte de cualquier archivo 3D sin preparación.** No admite directamente `SkinnedMeshRenderer`, blend shapes, deformación por huesos, geometría no manifold, caras degeneradas ni caras duplicadas. Para un personaje animado, primero haría falta trabajar con una copia horneada y estática o desarrollar integración con skinning.
- Es un prototipo visual de incisión. No modela elasticidad, fuerzas, sangrado, órganos ni un volumen de tejido. Las paredes son geometría adicional en V; una profundidad excesiva puede atravesar una superficie delgada o producir intersecciones entre heridas.
- La selección usa la **superficie original de referencia**, incluso en zonas donde ya se abrió una incisión. Esto mantiene estable el recorrido del mouse; no representa contacto físico con los bordes desplazados ni con el interior de la herida.
- No necesita collider para seleccionar el modelo y no actualiza sus colliders. Los colliders existentes siguen teniendo su geometría anterior. Tampoco considera objetos ajenos que tapen el modelo: la interacción se concentra en Target.
- El panel incluido bloquea el mouse sobre sí mismo. Si añades otra interfaz, suspende el corte con `SetCutting(false)` mientras se interactúa con ella.
- No se procesan triángulos que atraviesen los planos cercano o lejano de la cámara. Mantén el modelo completo dentro del rango de visión.
- Las costuras de posición se sueldan con una tolerancia pequeña. Superficies distintas que coincidan exactamente pueden unirse; usa un modelo limpio o desactiva Weld Seams. Si lo desactivas, las costuras importadas pueden funcionar como bordes independientes.
- El remallado y la reconstrucción se hacen en CPU. Empieza con unos pocos miles de triángulos y perfila en tu equipo. No está optimizado para modelos densos ni para rendimiento VR. El total visible incluye paredes adicionales y puede exceder Max Surface Triangles.
- No cambies Target ni Wound Material durante Play. Los parámetros de importación se vuelven a aplicar al pulsar Restaurar malla.

## Si aparece de color rosa/magenta

Ese color corresponde al shader de error de Unity. Revisa el material y los errores de compilación de su shader. En un proyecto URP usa, por ejemplo, un material con **Universal Render Pipeline/Lit**; en Built-in, uno con **Standard**. Haz lo mismo para Wound Material. Cambiar el algoritmo de corte no soluciona un shader incompatible.

## Comprobaciones incluidas

El archivo `Tests/Editor/SurfaceCutGeometryChecks.cs` añade el menú:

**Tools > Cirugia 3D > Ejecutar comprobaciones geometricas**

No necesita NUnit. Comprueba incisión interior, cruce y repetición de caras, cruces entre cortes, pliegues, costuras, reversión completa ante errores, apertura sin inversión, superficie cerrada y una secuencia determinista de múltiples trazos. Los resultados aparecen en Console. Son pruebas del núcleo geométrico; no sustituyen la prueba de cámara, materiales y entrada en Play Mode.

**Validación de esta entrega:** los tres archivos pasaron un analizador de sintaxis C#. Se comprobó numéricamente el límite de desplazamiento de apertura con 100 000 triángulos aleatorios. No había un compilador C# ni el editor Unity en el entorno de preparación; por tanto, la compilación contra Unity, la suite C# incluida y la prueba interactiva todavía deben ejecutarse en tu proyecto. No se presenta como una simulación quirúrgica validada.

Antes de usar tu modelo, prueba manualmente:

1. Esfera de Unity: corte corto, corte curvo y dos cortes que se crucen.
2. Rotar la esfera para comprobar que el lado posterior sigue intacto.
3. Soltar el botón, mover el cursor lejos y volver a cortar: no debe conectar ambos trazos.
4. Restaurar malla y verificar que recupera su forma.
5. Probar tu modelo con su textura para revisar costuras, escala y materiales.

## Referencias de Unity

- [Mesh.isReadable y Read/Write](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-isReadable.html)
- [MeshFilter.sharedMesh y la precaución al modificar assets compartidos](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MeshFilter-sharedMesh.html)
- [Shader de error de Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-error.html)
