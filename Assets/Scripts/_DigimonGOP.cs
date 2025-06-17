using Niantic.Lightship.Maps;
using Niantic.Lightship.Maps.Coordinates;
using Niantic.Lightship.Maps.Core.Coordinates;
using Niantic.Lightship.Maps.MapLayers.Components;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class _DigimonGOP : MonoBehaviour
{
    [SerializeField]
    private LightshipMapView lightshipMapView;
    private Vector3 Location = new Vector3(0.897385597f, 11.7783527f, 29.1192932f);
    //LatLng pontoNoMapa = new LatLng(-8.199222, -35.560096)(-8.005642, -35.037820);
    //LatLng pontoNoMapa = new LatLng(47.6198921203613, -122.353408813477);
    public Camera _mapCamera;
    public double raioEmMetros = 500.0;
    public LatLng centro; //= new LatLng(-8.005642, -35.037820);
    public LayerGameObjectPlacement prefab;
    [SerializeField]
    private List<GameObjectProbability> digimonLayers = new List<GameObjectProbability>();
    


    void Start()
    {
        InvokeRepeating("PlaceInstances", 5f, 30f);
        centro = lightshipMapView.MapCenter;
        LatLng coordenadasAleatorias = GerarCoordenadasAleatoriasNoCirculo();
        //for (int i = 0; i < 4; i++)
        //{
            //PlaceRandomDigimonInstance().PlaceInstance(GerarCoordenadasAleatoriasNoCirculo(), GerarRotacaoAleatoria());
            
        //}


        var ID = "Tsumemon";
        var location = coordenadasAleatorias;
        var cameraForward = _mapCamera.transform.forward;
        var forward = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        var rotation = Quaternion.LookRotation(forward);




    }
    private void PlaceInstances()
    {
        for (int i = 0; i < 5; i++)
        {
            PlaceRandomDigimonInstance().PlaceInstance(GerarCoordenadasAleatoriasNoCirculo(), GerarRotacaoAleatoria());
        }
    }
    private void Update()
    {
        centro = lightshipMapView.MapCenter;
    }
    public LatLng SceneToLatLng(Vector3 scenePosition)
    {
        var position = scenePosition + transform.position;
        return lightshipMapView.SceneToLatLng(position);
    }
    private Vector3 ConvertToScene(SerializableLatLng serializableLatLng)
    {
        return lightshipMapView.LatLngToScene(serializableLatLng);
    }
    private void PlaceObject(LatLng pos) => PlaceRandomDigimonInstance().PlaceInstance(pos);
    public LatLng GerarCoordenadasAleatoriasNoCirculo()
    {

        double anguloAleatorio = Random.Range(0f, 2f * Mathf.PI);

        int raioEmMetros = Random.Range(20, 80);
        double raioEmGraus = raioEmMetros / 111320.0;


        double latitude = centro.Latitude + raioEmGraus * Mathf.Sin((float)anguloAleatorio);
        double longitude = centro.Longitude + (raioEmGraus / Mathf.Cos((float)centro.Latitude)) * Mathf.Cos((float)anguloAleatorio);

        return new LatLng(latitude, longitude);
    }
    public Quaternion GerarRotacaoAleatoria()
    {

        Vector3 direcaoAleatoria = new Vector3(0, 0, Random.Range(-1f, 1f));


        direcaoAleatoria.Normalize();


        Quaternion rotacaoAleatoria = Quaternion.LookRotation(direcaoAleatoria);

        return rotacaoAleatoria;
    }
    private LayerGameObjectPlacement PlaceRandomDigimonInstance()
    {
        // Ordenar a lista pela probabilidade de forma decrescente
        digimonLayers.Sort((a, b) => b.probability.CompareTo(a.probability));

        // Selecionar aleatoriamente um índice com base nas probabilidades
        float randomValue = Random.value;
        float cumulativeProbability = 0f;
        int selectedLayerIndex = 0;

        for (int i = 0; i < digimonLayers.Count; i++)
        {
            cumulativeProbability += digimonLayers[i].probability;

            if (randomValue <= cumulativeProbability)
            {
                selectedLayerIndex = i;
                break;
            }
        }

        // Acessar o componente LayerGameObjectPlacement associado ao prefab
        LayerGameObjectPlacement layerPlacement = digimonLayers[selectedLayerIndex].gameObjectPlacement;

        return layerPlacement;
    }
}


