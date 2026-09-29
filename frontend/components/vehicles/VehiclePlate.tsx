/** A placa é o identificador que a equipe usa para encontrar um carro no pátio. */
export function VehiclePlate({ plate }: { plate: string }) {
  return (
    <span className="vehicle-plate" role="img" aria-label={`Placa ${plate}`}>
      <span className="vehicle-plate__band" aria-hidden="true">BRASIL</span>
      <span className="vehicle-plate__number" aria-hidden="true">{plate}</span>
    </span>
  );
}
