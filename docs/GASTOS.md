# Gastos

Además de los cobros, el salón anota lo que **paga**: el sueldo de una empleada, la cuota de autónomos, unas dietas, un viaje, el alquiler… Los gastos salen en el listado de **Visitas**, en su día y mezclados con las visitas por hora, y en el Excel en su propia hoja para mandarlo al asesor con el resto de cuentas.

## Cómo se usa

- **Nuevo gasto** (botón en `/admin/Visitas`, también en la pantalla vacía): tipo, fecha y hora, concepto (qué se ha pagado), importe, **cómo se pagó** (efectivo, tarjeta, transferencia, domiciliación o Bizum) y una nota opcional (número de factura, proveedor, para quién…). Desde la vista de un día propone ese día; si no, el de hoy.
  - *Anotar gasto* vuelve a la pantalla de la que se vino.
  - *Anotar y añadir otro* deja otro formulario en blanco del mismo día, para meter varios seguidos.
- **Corregir o borrar**: cada gasto del listado se abre con un clic. Al guardar o borrar se vuelve a la misma pantalla del listado.
- El importe es mayor que cero, hasta 999.999,99 € y con céntimos como mucho. El concepto tiene hasta 200 caracteres y la nota hasta 500.

## Tipos de gasto

Los tipos se editan en **Tipos de gasto** (`/admin/Gastos/Tipos`), a la que se llega con *Editar tipos* junto al desplegable del gasto (y al acabar se vuelve al gasto que se estaba anotando), con la fila «Tipos de gasto» del panel o con el botón de su tarjeta de gastos.

- **Añadir**: hasta 80 caracteres, sin repetir un nombre (no distingue mayúsculas). Sale al momento en el desplegable, antes de «Otros», que va siempre al final. Hasta 100 tipos.
- **Renombrar**: los gastos ya anotados salen con el nombre nuevo, en el listado, el panel y el Excel.
- **Archivar** un tipo que ya tiene gastos: deja de salir en los gastos nuevos, pero los suyos lo conservan (al corregir uno, el desplegable lo enseña como «(archivado)»). Se puede volver a activar.
- **Borrar**: solo un tipo sin gastos. Archivar y borrar piden confirmación.

De partida están: Personal (nóminas y Seguridad Social), Cuota de autónomos, Dietas, Viajes y desplazamientos, Material y productos, Suministros (luz, agua, teléfono…), Alquiler, Asesoría y profesionales, Formación, Publicidad, Reparaciones y mantenimiento, Seguros, Impuestos y tasas, Comisiones del banco y Otros.

## En el panel

El panel principal suma los gastos a lo que ya enseñaba:

- Una cifra más, **Gastos hoy**, en rojo y con signo menos: cuántos gastos y cuánto salió de la caja en efectivo. Lleva al listado del día.
- La tarjeta **Cobros y gastos** del mes: lo cobrado, los gastos y lo que queda (cobrado − gastos); los cinco tipos con más gasto, cada uno con una barra proporcional (los demás, juntos en «N tipos más»); y al pie cuántos gastos y visitas hubo, lo que queda por cobrar y el botón *Tipos de gasto*.
- **Nuevo gasto** junto a *Nueva visita* en la bienvenida.

## En el listado de visitas

El listado de un día, un mes, un año o todas las fechas es **una sola lista**, agrupada por día como siempre, con las visitas y los gastos ordenados por hora (lo más reciente primero; a la misma hora, la visita antes que el gasto). Los gastos se distinguen por el icono de cartera, el texto «Gasto · tipo», la forma de pago en rojo y el importe con signo menos.

- La cabecera de cada día enseña lo facturado en visitas y, si hay gastos, «· gastos −X €».
- Las cifras de arriba suman una más, **Gastos**: el total pagado en el periodo, cuántos gastos y «cobrado − gastos». En pantallas anchas van las cinco en una fila; en las medianas, tres y dos; en el móvil, de dos en dos con los gastos a todo el ancho.
- La ficha de un cliente solo enseña sus visitas: los gastos no son de ningún cliente.

## En el Excel

*Exportar* descarga el periodo que se está viendo (el archivo se llama «Visitas y gastos …» si hay gastos). Para no confundir **lo facturado** (lo que se ha hecho), **lo cobrado** (lo que ha entrado) y **los gastos** (lo que ha salido), cada concepto tiene su color en las cabeceras y en el resumen: facturado en azul, cobrado en verde, pendiente de cobro en naranja, gastos en rojo y resultado en morado.

| Hoja | Qué lleva |
|---|---|
| **Resumen** (la primera) | Cuatro bloques. *Facturado*: servicios, productos vendidos y total facturado. *Cobrado*: efectivo, tarjeta, total cobrado y lo pendiente de cobro. *Gastos*: cada tipo (en el orden de la pantalla de tipos), el total y el desglose por forma de pago; sin gastos, «No hubo gastos en este periodo.». *Resultado*: facturado − gastos, cobrado − gastos, y efectivo cobrado − gastos pagados en efectivo (lo que debería cuadrar con la caja). |
| **Visitas** | Una fila por visita. Encima de las cabeceras, una franja de color agrupa las columnas: *Facturado* (el importe de la visita), *Cobrado* (en efectivo, con tarjeta y el total cobrado) y *Pendiente* (lo que queda por cobrar). Fila de totales con fórmulas `SUM`. Es una tabla de Excel con filtros. |
| **Productos** | Si se vendieron productos, como siempre. |
| **Gastos** | Si hay gastos: fecha, hora, tipo, concepto, forma de pago, importe y nota de cada uno, bajo la franja roja «Gastos · lo que ha salido», con el total al final (fórmula `SUM`). |

Un periodo con gastos y sin visitas también se exporta (la hoja Visitas dice «No hubo visitas en este periodo.»). Uno sin nada avisa de que no hay visitas ni gastos que exportar.

## Base de datos

| Tabla | Qué guarda |
|---|---|
| `Gastos` | `Fecha` (datetime2, sin segundos), `TipoId` (su tipo en `TiposGasto`; un tipo con gastos no se puede borrar), `Concepto`, `Importe` (decimal(18,2), mayor que cero), `MedioPago` (1 efectivo, 2 tarjeta, 3 transferencia, 4 domiciliación, 5 Bizum), `Nota` y fechas de alta y cambio. Índices por fecha y por tipo; comprobaciones de importe y forma de pago. |
| `TiposGasto` | `Nombre` (único, hasta 80 caracteres), `Activo` (0 si está archivado), `Orden` (el del desplegable y el Excel; «Otros» lleva 999) y fechas de alta y cambio. Los tipos de partida tienen los números 1 a 14 y 99 («Otros»), los mismos que tenía la columna `Gastos.Tipo`. |

Migraciones:

- `GastosYNumerosDeSorteo` crea la tabla `Gastos`, junto con los números de los sorteos (ver [SORTEOS.md](SORTEOS.md#base-de-datos), donde está su estado). Solo añade: no toca las visitas ni los cobros.
- `TiposDeGastoTableroYResenas` hace editables los tipos: crea `TiposGasto` con los tipos de partida (los mismos números, así que cada gasto conserva el suyo), renombra `Gastos.Tipo` a `TipoId` con sus valores intactos y cambia la comprobación de valores por la clave ajena. En la misma migración van el talonario de los sorteos ([SORTEOS.md](SORTEOS.md#base-de-datos)) y las fechas de las reseñas ([CONTENIDO-Y-MFA.md](CONTENIDO-Y-MFA.md#opiniones-con-ticket)). No borra ni cambia ningún dato. Su vuelta atrás pasa a «Otros» los gastos de los tipos añadidos después (el esquema anterior no los conoce) y las reseñas ocultas a rechazadas. Aplicada en `PeluqueriaLucia_VistaPrevia` y probada también hacia atrás con datos (un tipo añadido pasa a «Otros» y vuelve a aplicarse sin perder ningún gasto); **pendiente en producción**, después de `GastosYNumerosDeSorteo` y `EfectivoEntregadoEnElCobro` (ver [AZURE.md](AZURE.md)).
