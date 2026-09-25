# Validación del prototipo

Fecha: 7 de septiembre de 2026.

El archivo probado es el mismo `Assets/CorteLibre/Runtime/PlanarCutMesh.cs` incluido en la entrega. Se compiló con Roslyn del SDK oficial .NET 8.0.424, fijando C# 7.3, y se ejecutó el ensamblado C# resultante. Las pruebas no requieren Unity ni paquetes NuGet.

## Casos comprobados

- Membrana inicial: 117 vértices materiales, 192 triángulos y área conservada.
- Diagonal arbitraria que inserta vértices fuera de la cuadrícula original.
- Repetición de una incisión sin crecimiento de la topología.
- Cruce de incisiones, conservando las restricciones anteriores.
- Cortes coincidentes con aristas y que pasan por vértices existentes.
- Segmentos que salen del rectángulo, recortados al dominio del tejido.
- Incisión que comienza y termina dentro de una sola cara, con vértice intermedio que permite abrirla.
- Trazo cerrado curvo y curva libre formada por 70 segmentos.
- Cincuenta segmentos arbitrarios con múltiples cruces, usando una semilla reproducible.
- Rechazo por presupuesto de triángulos, recuperando el estado anterior.

Después de cada corte se comprueban orientación de caras, área material, adyacencia de aristas, referencias de incisión y ausencia de bordes interiores espurios. En la geometría abierta se comprueban orientación positiva de los triángulos e identidad entre vértices visibles y materiales. Una comprobación independiente toma 99 muestras de cada segmento solicitado y verifica que la parte situada dentro de la membrana está cubierta por aristas de incisión.

Estos casos son regresiones concretas; no constituyen una demostración de robustez para todas las configuraciones degeneradas posibles. La implementación conserva el estado previo cuando sus validaciones detectan una operación inválida.

## Repetir las pruebas

En una máquina con .NET 8 SDK, desde la carpeta del ZIP:

```bash
dotnet run --project Tests/CoreTests.csproj -c Release
```

El proyecto referencia directamente el núcleo de `Assets`. La salida debe terminar en `SUCCESS`. También genera `curve_geometry.json` en el directorio de ejecución para inspeccionar la geometría. `Resultado_pruebas.txt` contiene la salida de la ejecución realizada para esta entrega.

## Pendiente de ejecutar dentro de Unity

No hay editor de Unity instalado en el entorno de creación. No se ha verificado aquí la importación de los scripts, la compilación con los ensamblados reales de Unity, el shader, el ratón, el HUD ni el rendimiento en Game view.

Comprobación práctica al importar: crea la escena desde Tools, pulsa Play, dibuja una curva interior y otra que la cruce, suelta y vuelve a hacer clic lejos para comprobar que no une trazos, desmarca el bool y mueve la esfera manualmente, y finalmente restaura la membrana. Comprueba que Console no muestre errores.
