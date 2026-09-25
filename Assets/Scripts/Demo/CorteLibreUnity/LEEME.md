# Corte libre en Unity

Prototipo C# de una membrana plana, inicialmente intacta, que puedes cortar con una esfera desde cualquier punto de su superficie. El trazo inserta vértices, subdivide triángulos y separa las conexiones de sus dos lados. Incluye una abertura visual ajustable y un bool para elegir entre ratón y movimiento propio.

## Ponerlo en marcha

1. Abre tu proyecto de Unity. El punto de partida previsto es un proyecto 3D con Built-in Render Pipeline, Unity 2022.3 o Unity 6. No necesitas paquetes externos para esta demo.
2. Descomprime `CorteLibreUnity.zip` y copia su carpeta `CorteLibreUnity/Assets/CorteLibre` dentro de `Assets` de tu proyecto. El resultado debe ser `Assets/CorteLibre/Runtime`, `Assets/CorteLibre/Editor` y `Assets/CorteLibre/Shaders`. Unity generará los archivos `.meta` al importar.
3. Espera a que Unity compile. Abre **Tools > Corte libre > Crear escena de prueba**. Si tu escena actual tiene cambios, Unity te ofrecerá guardarla.
4. El creador genera y guarda la escena, materiales y malla inicial en `Assets/CorteLibre/Generated`. Deja seleccionada la esfera para que encuentres sus controles en el Inspector.
5. Pulsa **Play**. En la ventana **Game**, mantén **clic izquierdo** y arrastra sobre el tejido, fuera del panel de controles. Verás nuevos triángulos y una abertura siguiendo tu recorrido.
6. Suelta el botón para terminar ese trazo. Puedes comenzar otro en cualquier punto. **Restaurar membrana** borra los cortes de esta ejecución.

La escena se crea desde código de editor para que Unity guarde los recursos en su propio formato. El paquete no requiere descargar modelos, texturas ni una escena externa.

## El bool que pediste

Está en `MouseScalpelSphere.cs`, componente de **Esfera bisturi - control mouse**:

```csharp
public bool followMouseOnClick = true;
```

Unity lo muestra como **Follow Mouse On Click** en el Inspector. También puedes cambiarlo con **Seguir mouse con clic** en el panel durante Play.

| Valor | Comportamiento |
|---|---|
| `true` | La esfera sigue al ratón solamente mientras mantienes clic izquierdo. Al soltar se queda quieta y termina el trazo. |
| `false` | El componente deja el `Transform` bajo tu control. La esfera corta al moverse en contacto con el tejido si `cuttingEnabled` está activo. |

`cuttingEnabled` controla el corte por separado. Puedes mover la esfera con el ratón y tener el corte desactivado. Un clic sobre el panel no empieza un corte.

Al iniciar un clic, la esfera se coloca bajo el cursor e inicia allí un nuevo trazo: no une ese punto con la posición del clic anterior. Un clic inmóvil coloca la herramienta; para producir una incisión hace falta desplazarla.

## Probarlo a tu manera

1. Desmarca **Follow Mouse On Click** y deja **Cutting Enabled** activado.
2. Durante Play, selecciona la esfera y muévela con la herramienta Move de la vista Scene, manteniéndola cerca del plano del tejido.
3. O hazla hija de tu bisturí/controlador y mueve ese objeto. Asigna la membrana en el campo **Membrane** de la esfera si montas tus propios objetos.

El `SphereCollider` proporciona el centro y el radio de contacto. No necesitas `Rigidbody`, `OnTriggerEnter`, un `MeshCollider` ni capas de colisión para este prototipo: el contacto se calcula directamente contra los triángulos abiertos.

Con los valores de la escena, el radio mundial de la esfera es `0.006` y su centro empieza `0.0015` por encima del plano. Si la alejas más que su radio, deja de tocar el tejido. Moverla sobre el fondo fuera de la membrana no corta.

Desde tu código puedes llamar a `SetCutting(true)` al apretar un gatillo y a `SetCutting(false)` al soltarlo. El segundo caso termina el segmento pendiente. `CancelStroke()` descarta la continuidad, útil antes de teletransportar la herramienta. Si otro script la mueve, hazlo en `Update` o configura el orden de ejecución para que el corte en `LateUpdate` lea la posición nueva.

## Qué hace cada recurso

| Archivo | Responsabilidad |
|---|---|
| `Runtime/PlanarCutMesh.cs` | Núcleo geométrico sin dependencias de Unity: insertar puntos, cortar caras, conservar incisiones y generar índices separados. |
| `Runtime/FreeCutMembrane.cs` | Generar la membrana, actualizar `MeshFilter`, dibujar las aristas y calcular contacto esfera–triángulo. |
| `Runtime/MouseScalpelSphere.cs` | Entrada del ratón, seguimiento opcional, muestreo del movimiento y continuidad de cada trazo. |
| `Runtime/CorteLibreHud.cs` | Panel de prueba con controles y recuentos. Puedes desactivarlo. |
| `Editor/CorteLibreDemoBuilder.cs` | Menú que crea y guarda la escena, cámara, base, esfera y materiales. |
| `Shaders/CorteLibreSurface.shader` | Material sencillo de color, sin iluminación, visible por ambos lados. |
| `Tests/Program.cs` | Pruebas ejecutables del núcleo C#; incluidas en el ZIP. |

## Cómo consigue trayectorias libres

Hay dos representaciones relacionadas. La **malla material** conserva las coordenadas originales y registra las aristas de incisión. La **malla de visualización**, que recibe el `MeshFilter`, contiene copias de los vértices donde los dos lados deben estar separados. Cambiar la apertura no borra el historial de cortes.

Para cada segmento del movimiento:

1. Se comprueba el contacto de la esfera con los triángulos de la superficie ya abierta. El punto se convierte a coordenadas materiales mediante interpolación baricéntrica.
2. Se insertan los extremos del segmento en la triangulación. Si caen dentro de una cara, esa cara se subdivide. Si caen en una arista compartida, se actualizan sus dos caras vecinas.
3. Se calculan los cruces con las aristas y se vuelven a triangular las partes a cada lado del segmento. Los cruces compartidos usan el mismo vértice material.
4. Las aristas nuevas que coinciden con el trazo se marcan como incisión. Si se subdivide una incisión anterior, sus dos partes heredan esa marca.
5. Al construir la malla visible, se unen los vértices solamente a través de conexiones sin cortar. Los lados de la incisión obtienen índices diferentes. Las puntas interiores siguen conectadas al tejido.
6. Se desplazan ligeramente los dos labios en direcciones opuestas. Para un corte aislado de una sola arista se añade un punto intermedio, de modo que pueda abrirse aunque sus puntas permanezcan unidas.

La esfera funciona como herramienta de contacto que dibuja una línea. Su radio sirve para detectar contacto y muestrear el movimiento; **no determina el ancho de la abertura ni elimina un disco de tejido**. El ancho lo controla `openingWidth`.

Una curva se aproxima mediante segmentos cortos entre muestras del movimiento. Esos segmentos pueden atravesar el interior de los triángulos: la trayectoria no tiene que coincidir con la cuadrícula inicial.

## Parámetros útiles

| Parámetro | Inicial | Qué cambia |
|---|---:|---|
| `width` / `depth` | `0.6` / `0.4` | Dimensiones locales X/Z. Pulsa restaurar después de cambiarlas. |
| `columns` / `rows` | `12` / `8` | Cuadrícula inicial: 117 vértices materiales y 192 triángulos. Pulsa restaurar tras cambiarla. |
| `openingWidth` | `0.004` | Separación nominal total entre labios, en unidades locales. Puedes cambiarla durante Play. |
| `minimumStrokeDistance` | `0.004` | Distancia material entre segmentos de corte. Reducirla da más muestras y genera más triángulos. |
| `showWireframe` | `true` | Muestra las aristas de los triángulos reales después del remallado. |
| `maxTriangles` | `16000` | Límite de caras. Al superarlo se rechaza la última operación; restaura para empezar de nuevo. Se aplica al restaurar. |
| `mouseHeightInRadii` | `0.25` | Altura del centro de la esfera sobre el plano como fracción de su radio. |
| `maximumTravelPerFrame` | `0.6` | Un salto mayor termina la continuidad y empieza una nueva muestra. |
| `maximumSamplesPerFrame` | `96` | Limita el muestreo de movimiento. Si el salto necesita más muestras, también se trata como un nuevo inicio. |

El ancho visible puede ser menor que `openingWidth`: se limita el desplazamiento según la altura de los triángulos vecinos para evitar invertir caras muy delgadas. Cerca de puntas, cruces o triángulos estrechos la abertura se estrecha. Aumentar mucho el parámetro no produce una retracción amplia del tejido.

Mantén la escala de la membrana uniforme y positiva, por ejemplo `(1,1,1)`, sin padres que introduzcan cizallamiento. Puedes trasladar y rotar el conjunto. Usa también escala uniforme en la esfera para que el volumen visual coincida con el contacto.

## Alcance y siguientes pasos

Esta entrega funciona sobre una **membrana plana sin grosor**, en su plano local XZ. Incluye diagonales, curvas por segmentos, cortes interiores, cruces y trazos cerrados. La malla cambia de verdad; no usa blendshapes, máscaras de transparencia ni borrado de triángulos para fingir el corte.

La apertura es geométrica y controlada por un parámetro. No hay elasticidad, fuerzas, sangrado ni simulación biomecánica. Un contorno cerrado puede separar conexiones de una región, pero esa región no obtiene automáticamente un `Rigidbody` ni cae por gravedad. Los cambios de Play no se guardan como un modelo nuevo.

El componente genera su propia malla: no se aplica directamente a un hígado importado, una superficie curva o un `SkinnedMeshRenderer`. Para esa etapa hacen falta intersecciones en 3D, conservación de atributos, grosor/paredes internas y, si buscas deformación física, un modelo de tejido conectado a la topología. El núcleo plano queda separado para que puedas estudiar primero el remallado.

Se recorren caras y se reconstruyen índices en CPU. El presupuesto evita crecer sin límite, pero no equivale a garantizar una tasa de fotogramas. Muchos cruces producen triángulos delgados. Los casos numéricos que detectan las validaciones revierten el último segmento y muestran el motivo en el panel.

## Si algo no se ve o no corta

- **No aparece el menú:** espera la compilación y revisa el primer error de Console. Conserva la carpeta `Editor` y todos los scripts del paquete.
- **No sigue al ratón:** usa la ventana Game, mantén clic izquierdo fuera del panel y activa el bool. La lectura usa `Mouse.current` si está activo el nuevo Input System, o `Input` con el sistema anterior.
- **La esfera se mueve sin cortar:** activa `cuttingEnabled`, comprueba la referencia `Membrane`, su escala y la distancia esfera–plano. Un clic quieto no crea una incisión.
- **No ves bien la abertura:** oculta los triángulos para inspeccionarla, amplía la ventana Game y alarga el trazo. La apertura está limitada localmente para conservar las caras.
- **Material rosa intenso por error de shader:** revisa Console. El shader incluido está orientado a Built-in; en URP puedes asignar materiales `Universal Render Pipeline/Unlit` a tejido, esfera, base y aristas conservando sus colores. HDRP no está cubierto por esta entrega.
- **Salta un tramo cuando mueves muy rápido:** se activó el límite de distancia o muestras por fotograma. Empieza con movimientos más lentos y ajusta esos límites según el rendimiento.

## Verificación y referencias

El núcleo geométrico se compiló y ejecutó como C# 7.3 sobre .NET 8. Pasó las pruebas incluidas; consulta `Docs/VALIDACION.md`. **No se ejecutó Unity en este entorno**, por lo que la importación de los scripts, la escena, la entrada real y el shader necesitan su prueba en tu editor.

`Docs/Geometria_verificada.png` se dibujó a partir de los vértices e índices producidos por el código C# probado. No es una captura de Unity.

El seguimiento utiliza un rayo desde la cámara y su intersección con el plano de la membrana; consulta las APIs oficiales de [Camera.ScreenPointToRay](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Camera.ScreenPointToRay.html) y [Plane.Raycast](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Plane.Raycast.html). Las dos rutas de entrada siguen la [guía oficial de migración de Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.1/manual/Migration.html).
