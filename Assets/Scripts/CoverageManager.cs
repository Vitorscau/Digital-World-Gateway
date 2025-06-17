using Niantic.Lightship.AR.VpsCoverage;

using Niantic.Lightship.Maps;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEngine;

using ArdkLatLng = Niantic.Lightship.AR.VpsCoverage.LatLng;
using MapsLatLng = Niantic.Lightship.Maps.Core.Coordinates.LatLng;

public class CoverageManager : MonoBehaviour
{

    [SerializeField]
    private LightshipMapView _mapView;

    [SerializeField] private LayerGameObjectPlacement _cubeGOP;

    private CoverageClient _coverageClient;

    [SerializeField]
    private int _queryRadius;

    [SerializeField]
    private LayerLineRenderer _areaBorder;

    private float tempoDecorrido;


    // Start is called before the first frame update
    void Start()
    {
        _coverageClient = CoverageClientFactory.Create();
        RequestAreasAroundMapCenter();
    }

    // Update is called once per frame
    void Update()
    {
        // Incrementa o tempo decorrido com o tempo do último frame
        tempoDecorrido += Time.deltaTime;

        // Verifica se passaram 2 minutos (120 segundos)
        if (tempoDecorrido >= 120f)
        {
            // Chama o método desejado
            RequestAreasAroundMapCenter();

            // Reinicia o contador de tempo
            tempoDecorrido = 0f;
        }
    }

    public void RequestAreasAroundMapCenter()
    {
        var mapCenter = ConvertArdkLatLng(_mapView.MapCenter);
        _coverageClient.TryGetCoverageAreas(mapCenter, _queryRadius, OnAreasResult);
    }

    public void OnAreasResult(CoverageAreasResult areaResult)
    {
        if (areaResult.Status != ResponseStatus.Success)
        {
            return;
        }

        foreach (var area in areaResult.Areas)
        {
            var areaID = area.LocalizationTargetIdentifiers[0];
            //_areaBorder.DrawLoop(ConvertMapsLatLng(area.Shape), areaID);
            PlaceObject(ConvertMapsLatLng(area.Centroid));
        
        }
    }
    private void PlaceObject(MapsLatLng pos) => _cubeGOP.PlaceInstance(pos);

    private ArdkLatLng ConvertArdkLatLng(MapsLatLng mapsLatLng)
    {
        return new ArdkLatLng(mapsLatLng.Latitude, mapsLatLng.Longitude);
    }

    private MapsLatLng ConvertMapsLatLng(ArdkLatLng ardkLatLng)
    {
        return new MapsLatLng(ardkLatLng.Latitude, ardkLatLng.Longitude);
    }

    private MapsLatLng[] ConvertMapsLatLng(ArdkLatLng[] ardkLatLng)
    {
        var results = new MapsLatLng[ardkLatLng.Length];
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = ConvertMapsLatLng(ardkLatLng[i]);
        }

        return results;
    }

    
}