using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using G = Cirugia3D.SurfaceCutGeometry;
using V = Cirugia3D.SurfaceCutGeometry.V;

namespace Cirugia3D
{
    [DisallowMultipleComponent]
    public sealed class ReferencedMeshSurgery : MonoBehaviour
    {
        // =========================================================
        // REFERENCIAS
        // =========================================================
        public SurgeryCanvas surgeryCanvas;
        public Vector3 midpoint = new Vector3(0,0,0);

        [Header("Referencias")]
        public MeshFilter target;

        [Tooltip("Centro de la esfera/punta que realiza la incision.")]
        public Transform scalpelTip;

        [Tooltip("Material de las paredes internas de la incision.")]
        public Material woundMaterial;

        [Tooltip("Vibration")]
        public ControllerVibration vib = new ControllerVibration();


        // =========================================================
        // INCISION
        // =========================================================

        [Header("Incision")]
        public bool cuttingEnabled = true;

        [Min(0)]
        public float openingWidth = 0.012f;

        [Min(0)]
        public float incisionDepth = 0.025f;


        // =========================================================
        // DETECCION DE LA PUNTA
        // =========================================================

        [Header("Scalpel Tip")]

        [Tooltip("Distancia maxima entre el centro de la punta y la superficie.")]
        [Min(0.0001f)]
        public float contactDistance = 0.01f;

        [Tooltip("Movimiento minimo antes de procesar otro tramo.")]
        [Min(0.0001f)]
        public float minimumStrokeDistance = 0.002f;

        [Tooltip("Separacion entre muestras del movimiento.")]
        [Min(0.0001f)]
        public float sampleSpacing = 0.003f;

        [Tooltip("Evita conectar teletransportes o saltos grandes del tracking.")]
        [Min(0.001f)]
        public float maximumTravelPerFrame = 0.15f;

        [Range(1, 64)]
        public int maximumSamplesPerFrame = 16;

        [Range(1, 256)]
        public int maximumSegmentsPerFrame = 64;


        // =========================================================
        // MALLA
        // =========================================================

        [Header("Malla")]

        public bool weldSeams = true;

        [Range(0.0000001f, 0.00001f)]
        public float toleranceRelative = 0.000001f;

        [Min(100)]
        public int maxSurfaceTriangles = 30000;


        // =========================================================
        // VARIABLES INTERNAS
        // =========================================================

        private G core;
        private G.Display display;

        private Mesh original;
        private Mesh runtime;

        private MeshFilter activeTarget;
        private MeshRenderer targetRenderer;

        private Material[] originalMaterials;

        private Vector3[] sourceVertices;
        private Vector3[] sourceNormals;
        private Vector2[] sourceUV;

        private int[] sourceIndices;
        private int[] sourceSubmesh;

        private int originalSubmeshCount;

        private bool hasUV;

        private bool hasTipAnchor;
        private Vector3 previousTipPosition;

        private Vector3[] worldPointCache;
        private int sourcePointCount;

        private float displayedOpening;
        private float displayedDepth;

        // 
        [Header("Medicion de Incision")]

        [Tooltip("Distancia minima requerida entre inicio y fin de la incision.")]
        public float incisionThreshold = 0.05f;

        [Tooltip("Se ejecuta cuando una incision terminada supera el threshold.")]
        public UnityEvent onIncisionAboveThreshold;


        // Informacion de la ultima incision
        public float LastIncisionDistance { get; private set; }

        private bool incisionActive = false;

        private Vector3 incisionStartPoint;
        private Vector3 incisionEndPoint;

        



        // =========================================================
        // START
        // =========================================================

        private void Start()
        {
            Initialize();
        }


        // =========================================================
        // INICIALIZACION
        // =========================================================

        private void Initialize()
        {
            try
            {
                activeTarget = target != null ? target : GetComponent<MeshFilter>();

                if (activeTarget == null) throw new Exception("Asigna el MeshFilter que quieres cortar.");

                if (scalpelTip == null)
                    throw new Exception("Asigna Scalpel Tip.");


                if (woundMaterial == null)
                    throw new Exception("Asigna Wound Material.");


                targetRenderer = activeTarget.GetComponent<MeshRenderer>();


                if (targetRenderer == null)
                    throw new Exception("Target necesita MeshRenderer.");


                original = activeTarget.sharedMesh;


                if (original == null)
                    throw new Exception("Target no tiene Mesh.");


                if (!original.isReadable) throw new Exception("Activa Read/Write en el modelo.");


                originalMaterials = targetRenderer.sharedMaterials;


                if (originalMaterials == null || originalMaterials.Length == 0)
                    throw new Exception("Target necesita al menos un material.");


                // -------------------------------------------------
                // Crear malla runtime
                // -------------------------------------------------

                runtime =
                    Instantiate(original);

                runtime.name =
                    original.name + " - Surgery Runtime";

                runtime.indexFormat =
                    IndexFormat.UInt32;

                runtime.MarkDynamic();


                if (
                    runtime.normals.Length !=
                    runtime.vertexCount
                )
                {
                    runtime.RecalculateNormals();
                }


                sourceVertices =
                    runtime.vertices;

                sourceNormals =
                    runtime.normals;

                sourceUV =
                    runtime.uv;


                hasUV =
                    sourceUV != null &&
                    sourceUV.Length ==
                    sourceVertices.Length;


                // -------------------------------------------------
                // Indices + submeshes
                // -------------------------------------------------

                originalSubmeshCount =
                    original.subMeshCount;


                var indices =
                    new List<int>();

                var submeshes =
                    new List<int>();


                for (
                    int s = 0;
                    s < originalSubmeshCount;
                    s++
                )
                {
                    if (
                        original.GetTopology(s) !=
                        MeshTopology.Triangles
                    )
                    {
                        throw new Exception(
                            "Solo se admiten triangulos."
                        );
                    }


                    int[] triangles =
                        original.GetTriangles(s);


                    indices.AddRange(
                        triangles
                    );


                    int triangleCount =
                        triangles.Length / 3;


                    for (
                        int i = 0;
                        i < triangleCount;
                        i++
                    )
                    {
                        submeshes.Add(s);
                    }
                }


                sourceIndices =
                    indices.ToArray();

                sourceSubmesh =
                    submeshes.ToArray();


                // -------------------------------------------------
                // Agregar material de la herida
                // -------------------------------------------------

                Material[] materials =
                    new Material[
                        originalSubmeshCount + 1
                    ];


                for (int i = 0; i < originalSubmeshCount; i++)
                {
                    materials[i] = originalMaterials[Mathf.Min(i, originalMaterials.Length - 1)];
                }


                materials[
                    originalSubmeshCount
                ] = woundMaterial;


                targetRenderer.sharedMaterials =
                    materials;

                activeTarget.sharedMesh =
                    runtime;


                ResetMesh();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Surgery: " + e.Message,
                    this
                );

                enabled = false;
            }
        }


        // =========================================================
        // RESET
        // =========================================================

        public void ResetMesh()
        {
            if (
                runtime == null ||
                original == null
            )
            {
                return;
            }


            CancelStroke();


            var positions =
                new V[
                    sourceVertices.Length
                ];


            for (
                int i = 0;
                i < sourceVertices.Length;
                i++
            )
            {
                positions[i] =
                    ToV(
                        sourceVertices[i]
                    );
            }


            double extent =
                original.bounds.size.magnitude;


            core =
                new G(
                    positions,
                    sourceIndices,

                    Math.Max(
                        0.000000000001,
                        extent *
                        toleranceRelative
                    ),

                    weldSeams
                );


            core.MaxFaces =
                Math.Max(
                    core.Faces.Count,
                    maxSurfaceTriangles
                );


            // Estos son los puntos originales de la superficie.
            // Los nuevos puntos creados durante los cortes
            // apareceran despues.

            sourcePointCount =
                core.Points.Count;


            worldPointCache =
                new Vector3[
                    sourcePointCount
                ];


            RebuildDisplay();
        }


        // =========================================================
        // UPDATE
        // =========================================================

        private void Update()
        {
            if (
                core == null ||
                scalpelTip == null ||
                activeTarget == null
            )
            {
                return;
            }


            core.MaxFaces =
                Math.Max(
                    core.Faces.Count,
                    maxSurfaceTriangles
                );


            // -----------------------------------------------------
            // Si cambia apertura o profundidad
            // -----------------------------------------------------

            if (
                !Mathf.Approximately(
                    displayedOpening,
                    openingWidth
                )
                ||
                !Mathf.Approximately(
                    displayedDepth,
                    incisionDepth
                )
            )
            {
                RebuildDisplay();
            }


            // -----------------------------------------------------
            // Corte desactivado
            // -----------------------------------------------------

            if (!cuttingEnabled)
            {
                CancelStroke();

                return;
            }


            Vector3 currentPosition =
                scalpelTip.position;


            // -----------------------------------------------------
            // Primer punto
            // -----------------------------------------------------

            if (!hasTipAnchor)
            {
                previousTipPosition =
                    currentPosition;

                hasTipAnchor =
                    true;

                return;
            }


            float distance =
                Vector3.Distance(
                    previousTipPosition,
                    currentPosition
                );


            // -----------------------------------------------------
            // Tracking jump
            // -----------------------------------------------------

            if (
                distance >
                maximumTravelPerFrame
            )
            {
                previousTipPosition =
                    currentPosition;

                return;
            }


            // -----------------------------------------------------
            // Movimiento demasiado pequeño
            // -----------------------------------------------------

            if (
                distance <
                minimumStrokeDistance
            )
            {
                return;
            }


            // -----------------------------------------------------
            // Procesar incision
            // -----------------------------------------------------

            ProcessTipStroke(previousTipPosition, currentPosition);


            previousTipPosition =
                currentPosition;
        }


        // =========================================================
        // PROCESAR MOVIMIENTO DE LA PUNTA
        // =========================================================

        private void ProcessTipStroke(Vector3 from, Vector3 to)
        {
            float travel = Vector3.Distance(from, to);

            if (travel <= 0.000001f) return;


            int samples =
                Mathf.Clamp(
                    Mathf.CeilToInt(
                        travel /
                        Mathf.Max(
                            sampleSpacing,
                            0.0001f
                        )
                    ),

                    1,
                    maximumSamplesPerFrame
                );


            // Transformar los puntos fuente a World Space
            // una sola vez para todo este movimiento.

            RefreshWorldPointCache();

            bool foundContact = false;

            Vector3 firstContactWorld = Vector3.zero;
            Vector3 lastContactWorld = Vector3.zero;

            var segments =
                new List<G.Segment>();


            bool hasPreviousContact =
                false;


            int previousSource =
                -1;


            V previousContact =
                new V();


            Vector3 previousSample =
                Vector3.zero;


            // =====================================================
            // MUESTREAR MOVIMIENTO DE LA ESFERA
            // =====================================================

            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;


                Vector3 samplePosition = Vector3.Lerp(from, to, t);

                int source;

                V contact;


                // -------------------------------------------------
                // Buscar punto más cercano de la superficie
                // -------------------------------------------------

                if (!TryGetClosestSurfacePoint(samplePosition, out source, out contact))
                {
                    hasPreviousContact = false;
                    continue;
                }

                Vector3 contactWorld = activeTarget.transform.TransformPoint(ToUnity(contact));


                if (!foundContact)
                {
                    firstContactWorld = contactWorld;
                    foundContact = true;
                }


                lastContactWorld = contactWorld;


                // -------------------------------------------------
                // Primer contacto
                // -------------------------------------------------

                if (!hasPreviousContact)
                {
                    previousSource =
                        source;

                    previousContact =
                        contact;

                    previousSample =
                        samplePosition;

                    hasPreviousContact =
                        true;

                    continue;
                }


                // -------------------------------------------------
                // Crear segmento
                // -------------------------------------------------

                AppendSurfaceTransition(

                    previousSample,
                    previousSource,
                    previousContact,

                    samplePosition,
                    source,
                    contact,

                    segments,

                    0
                );


                if (
                    segments.Count >=
                    maximumSegmentsPerFrame
                )
                {
                    break;
                }


                previousSample =
                    samplePosition;

                previousSource =
                    source;

                previousContact =
                    contact;
            }


            // =====================================================
            // SI YA NO HAY CONTACTO, TERMINAR LA INCISION
            // =====================================================

            if (!foundContact)
            {
                if (incisionActive)
                {
                    FinishIncision();
                }

                return;
            }


            // Si sí hay contacto pero todavía no existe
            // un segmento suficientemente grande, no cortar.
            if (segments.Count == 0)
            {
                return;
            }

            // =====================================================
            // REALIZAR EL CORTE
            // =====================================================

            bool changed =
                core.CutBatch(
                    segments
                );


            if (core.LastError != null)
            {
                Debug.LogWarning(core.LastError, this);
                return;
            }

            // Es el putno de cambio
            if (changed)
            {

                // ==========================================
                // COMENZÓ UNA NUEVA INCISIÓN
                // ==========================================

                if (!incisionActive)
                {
                    incisionActive = true;
                    incisionStartPoint = firstContactWorld;
                    incisionEndPoint = lastContactWorld;
                    Debug.Log("Inicio incision A: " + incisionStartPoint);
                }
                else
                {
                    // La incisión continúa.
                    incisionEndPoint = lastContactWorld;
                }


                // Vibration

                OVRInput.Controller controller = OVRInput.Controller.None;

                if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.1f)
                {
                    controller = OVRInput.Controller.RTouch;
                }
                else if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.1f)
                {
                    controller = OVRInput.Controller.LTouch;
                }

                if (controller != OVRInput.Controller.None)
                {
                    vib.Vibrate(0.05f, controller);
                }


                RebuildDisplay();
            }
        }



        private void FinishIncision()
        {
            if (!incisionActive)
                return;


            incisionActive = false;


            // ==========================================
            // DISTANCIA ENTRE A Y B
            // ==========================================

            LastIncisionDistance = Vector3.Distance(incisionStartPoint, incisionEndPoint);
            Debug.Log("Incision terminada." + "\nA: " + incisionStartPoint + "\nB: " + incisionEndPoint + "\nDistancia: " + LastIncisionDistance);
            surgeryCanvas.UpdateDistance(LastIncisionDistance);

            // ==========================================
            // COMPARAR CONTRA THRESHOLD
            // ==========================================

            float tolerance = .25f;

            if (LastIncisionDistance <= incisionThreshold * (1 + tolerance) && LastIncisionDistance >= incisionThreshold * (1 - tolerance))
            {
                Debug.Log("Incision correcta. Supero threshold.");
                midpoint = (incisionStartPoint + incisionEndPoint) / 2;
                // Aquí haces lo que quieras.
                onIncisionAboveThreshold?.Invoke();
            }
            else
            {
                ResetMesh();
                Debug.Log(
                    "Incision demasiado corta."
                );
            }
        }

        // =========================================================
        // BUSCAR PUNTO MÁS CERCANO
        // =========================================================

        private bool TryGetClosestSurfacePoint(Vector3 worldPosition, out int bestSource, out V bestLocalPoint)
        {
            bestSource = -1;
            bestLocalPoint = new V();
            float bestDistanceSquared = contactDistance * contactDistance;


            Vector3 bestWorldPoint =
                Vector3.zero;


            // -----------------------------------------------------
            // Revisar triángulos fuente
            // -----------------------------------------------------

            for (int i = 0; i < core.Sources.Count; i++)
            {
                G.Face face = core.Sources[i];


                Vector3 a = worldPointCache[face.A];

                Vector3 b = worldPointCache[face.B];

                Vector3 c = worldPointCache[face.C];


                Vector3 closest =
                    ClosestPointOnTriangle(
                        worldPosition,
                        a,
                        b,
                        c
                    );


                float distanceSquared =
                    (
                        closest -
                        worldPosition
                    ).sqrMagnitude;


                if (
                    distanceSquared <
                    bestDistanceSquared
                )
                {
                    bestDistanceSquared =
                        distanceSquared;

                    bestWorldPoint =
                        closest;

                    bestSource =
                        i;
                }
            }


            if (bestSource < 0)
                return false;


            Vector3 localPoint =
                activeTarget.transform
                    .InverseTransformPoint(
                        bestWorldPoint
                    );


            bestLocalPoint =
                ToV(
                    localPoint
                );


            return true;
        }


        // =========================================================
        // ACTUALIZAR POSICIONES WORLD
        // =========================================================

        private void RefreshWorldPointCache()
        {
            for (
                int i = 0;
                i < sourcePointCount;
                i++
            )
            {
                worldPointCache[i] =
                    activeTarget.transform
                        .TransformPoint(
                            ToUnity(
                                core.Points[i]
                            )
                        );
            }
        }


        // =========================================================
        // PASAR ENTRE TRIÁNGULOS
        // =========================================================

        private void AppendSurfaceTransition(

            Vector3 fromSample,
            int fromSource,
            V fromContact,

            Vector3 toSample,
            int toSource,
            V toContact,

            List<G.Segment> segments,

            int depth
        )
        {
            if (
                segments.Count >=
                maximumSegmentsPerFrame
            )
            {
                return;
            }


            // -----------------------------------------------------
            // Mismo triángulo
            // -----------------------------------------------------

            if (
                fromSource ==
                toSource
            )
            {
                AddSegment(
                    segments,
                    fromSource,
                    fromContact,
                    toContact
                );

                return;
            }


            // -----------------------------------------------------
            // Triángulos vecinos
            // -----------------------------------------------------

            V boundary;


            if (
                TryGetSharedBoundary(
                    fromSource,
                    toSource,
                    fromContact,
                    toContact,
                    out boundary
                )
            )
            {
                AddSegment(
                    segments,
                    fromSource,
                    fromContact,
                    boundary
                );


                AddSegment(
                    segments,
                    toSource,
                    boundary,
                    toContact
                );


                return;
            }


            // -----------------------------------------------------
            // Se saltaron varios triángulos.
            // Subdividir el movimiento.
            // -----------------------------------------------------

            if (depth >= 5)
                return;


            Vector3 middle =
                Vector3.Lerp(
                    fromSample,
                    toSample,
                    0.5f
                );


            int middleSource;

            V middleContact;


            if (
                !TryGetClosestSurfacePoint(
                    middle,
                    out middleSource,
                    out middleContact
                )
            )
            {
                return;
            }


            AppendSurfaceTransition(

                fromSample,
                fromSource,
                fromContact,

                middle,
                middleSource,
                middleContact,

                segments,

                depth + 1
            );


            AppendSurfaceTransition(

                middle,
                middleSource,
                middleContact,

                toSample,
                toSource,
                toContact,

                segments,

                depth + 1
            );
        }


        // =========================================================
        // AÑADIR SEGMENTO
        // =========================================================

        private void AddSegment(
            List<G.Segment> segments,
            int source,
            V from,
            V to
        )
        {
            if (
                segments.Count >=
                maximumSegmentsPerFrame
            )
            {
                return;
            }


            if (
                (to - from).Length <=
                core.Epsilon * 8
            )
            {
                return;
            }


            segments.Add(
                new G.Segment(
                    source,
                    from,
                    to
                )
            );
        }


        // =========================================================
        // FRONTERA ENTRE DOS TRIÁNGULOS
        // =========================================================

        private bool TryGetSharedBoundary(

            int sourceA,
            int sourceB,

            V pointA,
            V pointB,

            out V boundary
        )
        {
            boundary =
                new V();


            G.Face faceA =
                core.Sources[sourceA];

            G.Face faceB =
                core.Sources[sourceB];


            int[] a =
            {
                faceA.A,
                faceA.B,
                faceA.C
            };


            int[] b =
            {
                faceB.A,
                faceB.B,
                faceB.C
            };


            int shared1 =
                -1;

            int shared2 =
                -1;


            for (
                int i = 0;
                i < 3;
                i++
            )
            {
                for (
                    int j = 0;
                    j < 3;
                    j++
                )
                {
                    if (
                        a[i] !=
                        b[j]
                    )
                    {
                        continue;
                    }


                    if (shared1 < 0)
                    {
                        shared1 =
                            a[i];
                    }
                    else if (
                        a[i] !=
                        shared1
                    )
                    {
                        shared2 =
                            a[i];
                    }
                }
            }


            // -----------------------------------------------------
            // Comparten arista
            // -----------------------------------------------------

            if (
                shared1 >= 0 &&
                shared2 >= 0
            )
            {
                V edgeA =
                    core.Points[
                        shared1
                    ];


                V edgeB =
                    core.Points[
                        shared2
                    ];


                V middle =
                    (
                        pointA +
                        pointB
                    ) * 0.5;


                boundary =
                    ClosestPointOnSegment(
                        middle,
                        edgeA,
                        edgeB
                    );


                return true;
            }


            // -----------------------------------------------------
            // Comparten vértice
            // -----------------------------------------------------

            if (shared1 >= 0)
            {
                boundary =
                    core.Points[
                        shared1
                    ];

                return true;
            }


            return false;
        }


        // =========================================================
        // PUNTO MÁS CERCANO DE UNA ARISTA
        // =========================================================

        private V ClosestPointOnSegment(
            V point,
            V a,
            V b
        )
        {
            V ab =
                b - a;


            double denominator =
                G.Dot(
                    ab,
                    ab
                );


            if (
                denominator <=
                core.Epsilon *
                core.Epsilon
            )
            {
                return a;
            }


            double t =
                G.Dot(
                    point - a,
                    ab
                )
                /
                denominator;


            t =
                Math.Max(
                    0,
                    Math.Min(
                        1,
                        t
                    )
                );


            return
                a +
                ab * t;
        }


        // =========================================================
        // PUNTO MÁS CERCANO DE UN TRIÁNGULO
        // =========================================================

        private static Vector3 ClosestPointOnTriangle(
            Vector3 p,
            Vector3 a,
            Vector3 b,
            Vector3 c
        )
        {
            Vector3 ab =
                b - a;

            Vector3 ac =
                c - a;

            Vector3 ap =
                p - a;


            float d1 =
                Vector3.Dot(
                    ab,
                    ap
                );

            float d2 =
                Vector3.Dot(
                    ac,
                    ap
                );


            if (
                d1 <= 0 &&
                d2 <= 0
            )
            {
                return a;
            }


            Vector3 bp =
                p - b;


            float d3 =
                Vector3.Dot(
                    ab,
                    bp
                );

            float d4 =
                Vector3.Dot(
                    ac,
                    bp
                );


            if (
                d3 >= 0 &&
                d4 <= d3
            )
            {
                return b;
            }


            float vc =
                d1 * d4 -
                d3 * d2;


            if (
                vc <= 0 &&
                d1 >= 0 &&
                d3 <= 0
            )
            {
                float v =
                    d1 /
                    (
                        d1 -
                        d3
                    );


                return
                    a +
                    v * ab;
            }


            Vector3 cp =
                p - c;


            float d5 =
                Vector3.Dot(
                    ab,
                    cp
                );

            float d6 =
                Vector3.Dot(
                    ac,
                    cp
                );


            if (
                d6 >= 0 &&
                d5 <= d6
            )
            {
                return c;
            }


            float vb =
                d5 * d2 -
                d1 * d6;


            if (
                vb <= 0 &&
                d2 >= 0 &&
                d6 <= 0
            )
            {
                float w =
                    d2 /
                    (
                        d2 -
                        d6
                    );


                return
                    a +
                    w * ac;
            }


            float va =
                d3 * d6 -
                d5 * d4;


            if (
                va <= 0 &&
                d4 - d3 >= 0 &&
                d5 - d6 >= 0
            )
            {
                float w =
                    (d4 - d3)
                    /
                    (
                        (d4 - d3) +
                        (d5 - d6)
                    );


                return
                    b +
                    w * (c - b);
            }


            float denominatorInside =
                1f /
                (
                    va +
                    vb +
                    vc
                );


            float insideV =
                vb *
                denominatorInside;


            float insideW =
                vc *
                denominatorInside;


            return
                a +
                ab * insideV +
                ac * insideW;
        }


        // =========================================================
        // RECONSTRUIR MALLA
        // =========================================================

        private void RebuildDisplay()
        {
            if (core == null || runtime == null)
            {
                return;
            }

            display = core.BuildDisplay(Math.Max(0, openingWidth));

            var positions = new List<Vector3>();

            var normals = new List<Vector3>();

            List<Vector2> uvs = hasUV ? new List<Vector2>() : null;


            var submeshes = new List<int>[originalSubmeshCount + 1];


            for (int i = 0; i < submeshes.Length; i++)
            {
                submeshes[i] = new List<int>();
            }

            var materialNormals = new V[core.Points.Count];


            // =====================================================
            // SUPERFICIE
            // =====================================================

            for (int t = 0; t < core.Faces.Count; t++)
            {
                G.Face face = core.Faces[t];


                int ia = sourceIndices[face.Source * 3];


                int ib = sourceIndices[face.Source * 3 + 1];


                int ic = sourceIndices[face.Source * 3 + 2];


                for (int k = 0; k < 3; k++)
                {
                    V bary = G.Barycentric(core.Points[face[k]],
                            ToV(sourceVertices[ia]),
                            ToV(sourceVertices[ib]),
                            ToV(sourceVertices[ic])
                        );


                    float a = (float)bary.X;

                    float b = (float)bary.Y;

                    float c = (float)bary.Z;


                    Vector3 normal = (sourceNormals[ia] * a + sourceNormals[ib] * b + sourceNormals[ic] * c).normalized;


                    materialNormals[face[k]] = materialNormals[face[k]] + ToV(normal);

                    positions.Add(ToUnity(display.Positions[t * 3 + k]));

                    normals.Add(normal);


                    if (hasUV)
                    {
                        Vector2 uv = sourceUV[ia] * a + sourceUV[ib] * b + sourceUV[ic] * c;

                        uvs.Add(uv);
                    }


                    submeshes[sourceSubmesh[face.Source]
                    ].Add(positions.Count - 1);
                }
            }


            // =====================================================
            // PAREDES DE LA INCISION
            // =====================================================

            if (
                openingWidth > 0 &&
                incisionDepth > 0
            )
            {
                foreach (
                    ulong key
                    in core.Cuts
                )
                {
                    G.Edge edge =
                        display.Edges[key];


                    if (edge.F1 < 0)
                        continue;


                    for (
                        int side = 0;
                        side < 2;
                        side++
                    )
                    {
                        int faceIndex =
                            side == 0
                            ? edge.F0
                            : edge.F1;


                        int edgeIndex =
                            side == 0
                            ? edge.K0
                            : edge.K1;


                        int ca =
                            faceIndex * 3 +
                            edgeIndex;


                        int cb =
                            faceIndex * 3 +
                            (
                                edgeIndex + 1
                            ) % 3;


                        int va =
                            core.Faces[
                                faceIndex
                            ][
                                edgeIndex
                            ];


                        int vb =
                            core.Faces[
                                faceIndex
                            ][
                                (
                                    edgeIndex + 1
                                ) % 3
                            ];


                        V topA =
                            display.Positions[
                                ca
                            ];


                        V topB =
                            display.Positions[
                                cb
                            ];


                        V bottomA =
                            core.Points[va]
                            -
                            materialNormals[
                                va
                            ].Unit
                            *
                            (
                                display.Separated[ca]
                                ? incisionDepth
                                : 0
                            );


                        V bottomB =
                            core.Points[vb]
                            -
                            materialNormals[
                                vb
                            ].Unit
                            *
                            (
                                display.Separated[cb]
                                ? incisionDepth
                                : 0
                            );


                        AddWall(

                            topA,
                            bottomA,
                            topB,

                            positions,
                            normals,
                            uvs,

                            submeshes[
                                originalSubmeshCount
                            ]
                        );


                        AddWall(

                            topB,
                            bottomA,
                            bottomB,

                            positions,
                            normals,
                            uvs,

                            submeshes[
                                originalSubmeshCount
                            ]
                        );
                    }
                }
            }


            // =====================================================
            // ENVIAR RESULTADO A UNITY
            // =====================================================

            runtime.Clear();


            runtime.SetVertices(
                positions
            );


            runtime.SetNormals(
                normals
            );


            if (hasUV)
            {
                runtime.SetUVs(
                    0,
                    uvs
                );
            }


            runtime.subMeshCount =
                submeshes.Length;


            for (
                int i = 0;
                i < submeshes.Length;
                i++
            )
            {
                runtime.SetTriangles(
                    submeshes[i],
                    i,
                    false
                );
            }


            runtime.RecalculateBounds();


            displayedOpening =
                openingWidth;


            displayedDepth =
                incisionDepth;
        }


        // =========================================================
        // PARED INTERNA
        // =========================================================

        private void AddWall(

            V a,
            V b,
            V c,

            List<Vector3> positions,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices
        )
        {
            V normal =
                G.Cross(
                    b - a,
                    c - a
                );


            if (
                normal.Length <=
                core.Epsilon *
                core.Epsilon
            )
            {
                return;
            }


            V[] points =
            {
                a,
                b,
                c
            };


            for (
                int i = 0;
                i < 3;
                i++
            )
            {
                positions.Add(
                    ToUnity(
                        points[i]
                    )
                );


                normals.Add(
                    ToUnity(
                        normal.Unit
                    )
                );


                if (hasUV)
                {
                    uvs.Add(
                        i == 0
                        ? new Vector2(0, 0)
                        : i == 1
                            ? new Vector2(0, 1)
                            : new Vector2(1, 0)
                    );
                }


                indices.Add(
                    positions.Count - 1
                );
            }
        }


        // =========================================================
        // ACTIVAR / DESACTIVAR CORTE
        // =========================================================

        public void SetCutting(
            bool value
        )
        {
            cuttingEnabled =
                value;


            CancelStroke();
        }


        private void CancelStroke()
        {
            hasTipAnchor =
                false;
        }


        // =========================================================
        // CONVERSIONES
        // =========================================================

        private static V ToV(
            Vector3 p
        )
        {
            return
                new V(
                    p.x,
                    p.y,
                    p.z
                );
        }


        private static Vector3 ToUnity(
            V p
        )
        {
            return
                new Vector3(
                    (float)p.X,
                    (float)p.Y,
                    (float)p.Z
                );
        }


        // =========================================================
        // CLEANUP
        // =========================================================

        private void OnDisable()
        {
            CancelStroke();
        }


        private void OnDestroy()
        {
            if (
                activeTarget != null &&
                activeTarget.sharedMesh ==
                runtime
            )
            {
                activeTarget.sharedMesh =
                    original;


                if (
                    targetRenderer != null &&
                    originalMaterials != null
                )
                {
                    targetRenderer.sharedMaterials =
                        originalMaterials;
                }
            }


            if (runtime != null)
            {
                Destroy(
                    runtime
                );
            }
        }
    }
}