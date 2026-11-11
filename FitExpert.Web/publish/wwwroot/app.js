/* =====================================================================
   FitExpert · cliente web del sistema experto
   Inicio de sesión por perfil, app del usuario final y panel del
   administrador. Todo el conocimiento llega del servicio REST.
   ===================================================================== */
(() => {
  "use strict";

  // ------------------------------------------------------------- iconos
  const ICONOS = {
    pesa: '<path d="M6.5 6.5v11M17.5 6.5v11M3 9.5v5M21 9.5v5M6.5 12h11"/>',
    atleta: '<circle cx="12" cy="5" r="2"/><path d="M12 7v6l-3 7M12 13l3 7M7 10l5-2 5 2"/>',
    escudo: '<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/><path d="M9 12l2 2 4-4"/>',
    ojo: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    casa: '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
    pulso: '<path d="M3 12h4l3-8 4 16 3-8h4"/>',
    reloj: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    salir: '<path d="M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10"/>',
    grafica: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
    reglas: '<path d="M8 6h13M8 12h13M8 18h13"/><path d="M3 6l1 1 2-2M3 12l1 1 2-2M3 18l1 1 2-2"/>',
    grupo: '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3.5 2.7-6 6-6s6 2.5 6 6"/><circle cx="17" cy="9" r="2.5"/><path d="M16 14c3 0 5 2 5 5"/>',
    cerrar: '<path d="M6 6l12 12M18 6L6 18"/>',
    check: '<path d="M5 12l5 5 9-10"/>',
    llama: '<path d="M12 3c1 4 5 5 5 10a5 5 0 01-10 0c0-3 2-4 2-7 1.5 1 3 2.5 3 5"/>',
    plato: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="4"/>',
    bombilla: '<path d="M9 18h6M10 21h4M12 3a6 6 0 00-4 10.5c.8.8 1 1.5 1 2.5h6c0-1 .2-1.7 1-2.5A6 6 0 0012 3z"/>',
    gota: '<path d="M12 3s6 7 6 11a6 6 0 01-12 0c0-4 6-11 6-11z"/>',
    luna: '<path d="M20 14A8 8 0 1110 4a6 6 0 0010 10z"/>',
    calendario: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/>',
    flecha: '<path d="M5 12h14M13 6l6 6-6 6"/>',
    atras: '<path d="M19 12H5M11 6l-6 6 6 6"/>',
    mas: '<path d="M12 5v14M5 12h14"/>',
    chispa: '<path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z"/><path d="M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z"/>',
    enviar: '<path d="M4 12l16-8-6 16-2-7z"/>'
  };

  function svg(nombre) {
    return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONOS[nombre] || ""}</svg>`;
  }

  function pintarIconos(raiz = document) {
    raiz.querySelectorAll("[data-icono]").forEach((n) => {
      if (!n.querySelector("svg")) n.insertAdjacentHTML("afterbegin", svg(n.dataset.icono));
    });
  }

  // ------------------------------------------------------------- utilidades
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];

  /** Crea un elemento. Los textos se insertan siempre como texto, nunca como HTML. */
  function el(etiqueta, atributos = {}, ...hijos) {
    const nodo = document.createElement(etiqueta);
    for (const [clave, valor] of Object.entries(atributos)) {
      if (valor === null || valor === undefined || valor === false) continue;
      if (clave === "class") nodo.className = valor;
      else if (clave === "style") nodo.setAttribute("style", valor);
      else if (clave.startsWith("on")) nodo.addEventListener(clave.slice(2), valor);
      else if (clave === "icono") nodo.insertAdjacentHTML("afterbegin", svg(valor));
      else nodo.setAttribute(clave, valor === true ? "" : valor);
    }
    for (const hijo of hijos.flat()) {
      if (hijo === null || hijo === undefined || hijo === false) continue;
      nodo.append(hijo instanceof Node ? hijo : document.createTextNode(String(hijo)));
    }
    return nodo;
  }

  const icono = (nombre, clase = "") => el("span", { class: clase, icono: nombre });
  const numero = (v) => Number(v).toLocaleString("es-CO", { maximumFractionDigits: 1 });
  const fechaLarga = (v) => new Date(v).toLocaleDateString("es-CO", { day: "numeric", month: "long", year: "numeric" });

  const AREAS = {
    SEGURIDAD: { nombre: "Seguridad", color: "#e11d2e" },
    DISPONIBILIDAD: { nombre: "Disponibilidad", color: "#f59e0b" },
    RECUPERACION: { nombre: "Recuperación", color: "#3b82f6" },
    RESTRICCION: { nombre: "Restricción", color: "#a855f7" },
    ENTRENAMIENTO: { nombre: "Entrenamiento", color: "#22c55e" }
  };
  function chipArea(codigo) {
    const area = AREAS[(codigo || "").toUpperCase()] || { nombre: codigo, color: "#71717a" };
    return el("span", { class: "chip", style: `background:${area.color}22;color:${area.color}` }, area.nombre);
  }
  const iniciales = (nombre) => (nombre || "?").split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join("");

  function avisar(texto, esError = false) {
    const aviso = el("div", { class: "aviso" + (esError ? " error-aviso" : "") }, texto);
    $("#avisos").append(aviso);
    setTimeout(() => aviso.remove(), 4200);
  }

  const cargando = (texto) => el("div", { class: "cargando" }, texto);

  /** Reemplaza el contenido de un nodo, ignorando los elementos vacíos (null). */
  function poner(nodo, ...hijos) {
    nodo.replaceChildren(...hijos.flat().filter((h) => h !== null && h !== undefined && h !== false));
  }

  // ------------------------------------------------------------- sesión
  const CLAVE_SESION = "fitexpert.sesion";
  const estado = {
    token: null, usuario: null, perfil: "USUARIO",
    preguntas: [], respuestas: {}, paso: 0, plan: null, historial: []
  };

  function guardarSesion(sesion) {
    estado.token = sesion.token;
    estado.usuario = sesion.usuario;
    try { localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion)); } catch { /* almacenamiento no disponible */ }
  }

  function leerSesion() {
    try {
      const sesion = JSON.parse(localStorage.getItem(CLAVE_SESION) || "null");
      if (sesion && new Date(sesion.expira) > new Date()) return sesion;
    } catch { /* sesión dañada */ }
    return null;
  }

  function cerrarSesion(mensaje) {
    estado.token = null;
    estado.usuario = null;
    estado.plan = null;
    estado.respuestas = {};
    estado.paso = 0;
    estado.historial = [];
    try { localStorage.removeItem(CLAVE_SESION); } catch { /* sin almacenamiento */ }
    mostrarPantalla("login");
    if (mensaje) $("#error-login").textContent = mensaje;
  }

  async function api(ruta, { metodo = "GET", cuerpo } = {}) {
    const encabezados = { Accept: "application/json" };
    if (cuerpo !== undefined) encabezados["Content-Type"] = "application/json";
    if (estado.token) encabezados.Authorization = "Bearer " + estado.token;

    const respuesta = await fetch(ruta, {
      method: metodo,
      headers: encabezados,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo)
    });

    let datos = null;
    try { datos = await respuesta.json(); } catch { datos = null; }

    if (respuesta.status === 401 && estado.token) {
      cerrarSesion((datos && datos.error) || "Su sesión expiró. Inicie sesión de nuevo.");
      throw new Error("Sesión expirada.");
    }
    if (!respuesta.ok) {
      throw new Error((datos && (datos.error || datos.detail || datos.title)) || `El servicio respondió con el estado ${respuesta.status}.`);
    }
    return datos;
  }

  function mostrarPantalla(nombre) {
    $("#pantalla-login").hidden = nombre !== "login";
    $("#pantalla-usuario").hidden = nombre !== "usuario";
    $("#pantalla-admin").hidden = nombre !== "admin";
    window.scrollTo({ top: 0 });
  }

  function iniciarSegunRol() {
    if (estado.usuario.rol === "ADMINISTRADOR") {
      $("#avatar-admin").textContent = iniciales(estado.usuario.nombreCompleto);
      $("#nombre-admin").textContent = estado.usuario.nombreCompleto;
      mostrarPantalla("admin");
      irAdmin("resumen");
    } else {
      $("#avatar-usuario").textContent = iniciales(estado.usuario.nombreCompleto);
      $("#avatar-usuario").title = estado.usuario.nombreCompleto;
      mostrarPantalla("usuario");
      irUsuario("inicio");
    }
  }

  // ================================================================ LOGIN
  function elegirPerfil(perfil) {
    estado.perfil = perfil;
    $$(".segmento button").forEach((b) => b.classList.toggle("activo", b.dataset.perfil === perfil));
    $("#enlace-registro").hidden = perfil === "ADMINISTRADOR";
    $("#boton-login").textContent = perfil === "ADMINISTRADOR" ? "Ingresar al panel" : "Empezar a entrenar";
    $("#error-login").textContent = "";
  }

  $$(".segmento button").forEach((b) => b.addEventListener("click", () => elegirPerfil(b.dataset.perfil)));

  $(".ver-clave").addEventListener("click", () => {
    const campo = $("#form-login [name=clave]");
    campo.type = campo.type === "password" ? "text" : "password";
  });

  $$(".demo button").forEach((b) => b.addEventListener("click", () => {
    const esAdmin = b.dataset.demo === "admin";
    elegirPerfil(esAdmin ? "ADMINISTRADOR" : "USUARIO");
    $("#form-login [name=usuario]").value = esAdmin ? "admin" : "usuario";
    $("#form-login [name=clave]").value = esAdmin ? "Admin2026*" : "Usuario2026*";
  }));

  $("#form-login").addEventListener("submit", async (evento) => {
    evento.preventDefault();
    const datos = new FormData(evento.target);
    const boton = $("#boton-login");
    const textoOriginal = boton.textContent;
    $("#error-login").textContent = "";
    boton.disabled = true;
    boton.textContent = "Verificando…";

    try {
      const sesion = await api("/api/auth/login", {
        metodo: "POST",
        cuerpo: { usuario: datos.get("usuario"), clave: datos.get("clave"), perfil: estado.perfil }
      });
      guardarSesion(sesion);
      evento.target.reset();
      iniciarSegunRol();
    } catch (error) {
      $("#error-login").textContent = error.message;
    } finally {
      boton.disabled = false;
      boton.textContent = textoOriginal;
    }
  });

  $("#ir-registro").addEventListener("click", (e) => { e.preventDefault(); $("#bloque-ingreso").hidden = true; $("#bloque-registro").hidden = false; });
  $("#ir-ingreso").addEventListener("click", (e) => { e.preventDefault(); $("#bloque-registro").hidden = true; $("#bloque-ingreso").hidden = false; });

  $("#form-registro").addEventListener("submit", async (evento) => {
    evento.preventDefault();
    const datos = Object.fromEntries(new FormData(evento.target));
    $("#error-registro").textContent = "";
    try {
      const sesion = await api("/api/auth/registro", { metodo: "POST", cuerpo: datos });
      guardarSesion(sesion);
      evento.target.reset();
      $("#bloque-registro").hidden = true;
      $("#bloque-ingreso").hidden = false;
      iniciarSegunRol();
      avisar("¡Tu cuenta está lista! Empieza con tu primera evaluación.");
    } catch (error) {
      $("#error-registro").textContent = error.message;
    }
  });

  $$("[data-salir]").forEach((b) => b.addEventListener("click", () => cerrarSesion()));

  // ======================================================== USUARIO FINAL
  function irUsuario(vista) {
    $$("#nav-usuario button").forEach((b) => b.classList.toggle("activo", b.dataset.vista === vista));
    $$("#pantalla-usuario .vista").forEach((v) => v.classList.toggle("activa", v.id === "vista-" + vista));
    window.scrollTo({ top: 0 });
    if (vista === "inicio") renderInicio();
    if (vista === "evaluacion") renderEvaluacion();
    if (vista === "plan") renderPlan();
    if (vista === "historial") renderHistorial();
  }
  $$("#nav-usuario button").forEach((b) => b.addEventListener("click", () => irUsuario(b.dataset.vista)));

  async function cargarHistorial() {
    estado.historial = await api("/api/app/evaluaciones");
    return estado.historial;
  }

  // ------------------------------------------------------------- inicio
  const HABITOS = [
    { id: "agua", icono: "gota", titulo: "Hidratación", detalle: "35 ml por kg de peso" },
    { id: "sueno", icono: "luna", titulo: "Sueño", detalle: "7 a 9 horas" },
    { id: "entreno", icono: "pesa", titulo: "Entrenamiento", detalle: "Sesión del plan" },
    { id: "proteina", icono: "plato", titulo: "Proteína", detalle: "En cada comida" }
  ];

  const claveHabitos = () => `fitexpert.habitos.${estado.usuario.usuario}.${new Date().toISOString().slice(0, 10)}`;
  function leerHabitos() {
    try { return JSON.parse(localStorage.getItem(claveHabitos()) || "{}"); } catch { return {}; }
  }
  function guardarHabitos(valores) {
    try { localStorage.setItem(claveHabitos(), JSON.stringify(valores)); } catch { /* sin almacenamiento */ }
  }

  async function renderInicio() {
    const vista = $("#vista-inicio");
    const nombre = (estado.usuario.nombreCompleto || "").split(" ")[0];
    poner(vista, 
      el("h1", { class: "titulo" }, "Hola, ", el("span", {}, nombre)),
      el("p", { class: "subtitulo" }, "Constancia hoy, resultados mañana."),
      cargando("Cargando tu progreso…")
    );

    let historial = [];
    try { historial = await cargarHistorial(); } catch (error) { avisar(error.message, true); }

    const ultimo = historial[0];
    const diasActivos = new Set(historial.map((h) => new Date(h.fecha).toDateString())).size;

    const hero = ultimo
      ? el("div", { class: "tarjeta hero-plan" },
          el("div", { class: "anillo", style: `--p:${ultimo.peso}` }, el("span", {}, `${Math.round(ultimo.peso)}%`)),
          el("div", {},
            el("span", { class: "etiqueta" }, "Tu plan actual"),
            el("h2", {}, `${ultimo.codigoHipotesis} · ${ultimo.nombreHipotesis}`),
            el("p", {}, `Generado el ${fechaLarga(ultimo.fecha)}`)),
          el("button", { class: "boton primario", onclick: () => abrirEvaluacion(ultimo.idEvaluacion) }, "Ver mi plan", icono("flecha")))
      : el("div", { class: "tarjeta hero-plan" },
          el("div", { class: "anillo", style: "--p:0" }, el("span", {}, "0%")),
          el("div", {},
            el("span", { class: "etiqueta" }, "Empieza aquí"),
            el("h2", {}, "Aún no tienes un plan"),
            el("p", {}, "Responde 9 preguntas y el sistema experto armará tu rutina y tu dieta.")),
          el("button", { class: "boton primario", onclick: () => irUsuario("evaluacion") }, "Hacer evaluación", icono("flecha")));

    const datos = el("div", { class: "rejilla tres" },
      tarjetaDato("pulso", historial.length, "Evaluaciones realizadas"),
      tarjetaDato("llama", diasActivos, "Días activos"),
      tarjetaDato("calendario", ultimo ? new Date(ultimo.fecha).toLocaleDateString("es-CO", { day: "numeric", month: "short" }) : "—", "Última evaluación"));

    const valores = leerHabitos();
    const contador = el("span");
    const barra = el("i");
    const actualizarProgreso = () => {
      const hechos = HABITOS.filter((h) => valores[h.id]).length;
      contador.textContent = `${hechos} de ${HABITOS.length} hábitos de hoy`;
      barra.style.width = `${(hechos / HABITOS.length) * 100}%`;
    };

    const habitos = el("div", { class: "habitos" }, HABITOS.map((h) => {
      const boton = el("button", { class: "habito" + (valores[h.id] ? " hecho" : ""), type: "button" },
        el("span", { class: "check", icono: "check" }),
        el("span", {}, el("b", {}, h.titulo), el("small", {}, h.detalle)));
      boton.addEventListener("click", () => {
        valores[h.id] = !valores[h.id];
        boton.classList.toggle("hecho", valores[h.id]);
        guardarHabitos(valores);
        actualizarProgreso();
      });
      return boton;
    }));
    actualizarProgreso();

    poner(vista, 
      el("h1", { class: "titulo" }, "Hola, ", el("span", {}, nombre)),
      el("p", { class: "subtitulo" }, "Constancia hoy, resultados mañana."),
      hero,
      el("h2", { class: "seccion" }, "Tu progreso"),
      datos,
      el("h2", { class: "seccion" }, "Hábitos de hoy"),
      habitos,
      el("div", { class: "progreso-habitos" }, contador, el("div", { class: "barra-fina" }, barra))
    );
  }

  function tarjetaDato(nombreIcono, valor, texto) {
    return el("div", { class: "tarjeta dato" },
      el("span", { class: "ico-dato", icono: nombreIcono }),
      el("strong", {}, String(valor)),
      el("span", {}, texto));
  }

  // ------------------------------------------------------------- evaluación
  async function renderEvaluacion() {
    const vista = $("#vista-evaluacion");
    if (!estado.preguntas.length) {
      poner(vista, cargando("Consultando la base de conocimiento…"));
      try {
        estado.preguntas = await api("/api/app/preguntas");
      } catch (error) {
        poner(vista, estadoVacio("pulso", "No se pudo cargar el cuestionario", error.message,
          el("button", { class: "boton primario", onclick: () => renderEvaluacion() }, "Reintentar")));
        return;
      }
    }
    renderPaso();
  }

  function renderPaso() {
    const vista = $("#vista-evaluacion");
    const total = estado.preguntas.length;
    const pregunta = estado.preguntas[estado.paso];
    const elegida = estado.respuestas[pregunta.idPregunta];
    const respondidas = Object.keys(estado.respuestas).length;
    const esUltima = estado.paso === total - 1;

    const opciones = el("div", { class: "opciones" }, pregunta.opciones.map((opcion, i) =>
      el("button", {
        class: "opcion" + (elegida === opcion.idHecho ? " elegida" : ""),
        type: "button",
        onclick: () => {
          estado.respuestas[pregunta.idPregunta] = opcion.idHecho;
          if (!esUltima) {
            const pasoActual = estado.paso;
            renderPaso();
            setTimeout(() => {
              if (estado.paso === pasoActual && estado.paso < total - 1) { estado.paso++; renderPaso(); }
            }, 220);
          } else {
            renderPaso();
          }
        }
      }, el("span", { class: "letra" }, String.fromCharCode(65 + i)), opcion.texto)));

    const atras = el("button", { class: "boton fantasma", type: "button", disabled: estado.paso === 0, onclick: () => { estado.paso--; renderPaso(); } }, icono("atras"), "Atrás");
    const siguiente = esUltima
      ? el("button", { class: "boton primario", type: "button", disabled: respondidas < total, onclick: generarPlan }, "Ver mi plan", icono("flecha"))
      : el("button", { class: "boton primario", type: "button", disabled: elegida === undefined && pregunta.obligatoria, onclick: () => { estado.paso++; renderPaso(); } }, "Siguiente", icono("flecha"));

    const puntos = el("div", { class: "puntos" }, estado.preguntas.map((p, i) =>
      el("i", { class: (estado.respuestas[p.idPregunta] !== undefined ? "lleno" : "") + (i === estado.paso ? " actual" : "") })));

    poner(vista, el("div", { class: "asistente" },
      el("div", { class: "asistente-cabecera" },
        el("span", {}, `Pregunta ${estado.paso + 1} de ${total}`),
        el("span", {}, `${Math.round((respondidas / total) * 100)} % completado`)),
      el("div", { class: "barra-fina" }, el("i", { style: `width:${(respondidas / total) * 100}%` })),
      el("span", { class: "pregunta-num" }, pregunta.codigo),
      el("h2", { class: "pregunta-texto" }, pregunta.texto),
      opciones,
      el("div", { class: "asistente-acciones" }, atras, siguiente),
      puntos));
  }

  async function generarPlan(evento) {
    const boton = evento.currentTarget;
    boton.disabled = true;
    boton.textContent = "Analizando tu perfil…";
    try {
      const hechos = estado.preguntas.map((p) => estado.respuestas[p.idPregunta]).filter((id) => id !== undefined);
      estado.plan = await api("/api/app/evaluar", { metodo: "POST", cuerpo: { hechos } });
      estado.respuestas = {};
      estado.paso = 0;
      avisar("¡Plan generado! Revisa tu rutina y tu dieta.");
      irUsuario("plan");
    } catch (error) {
      avisar(error.message, true);
      boton.disabled = false;
      boton.textContent = "Ver mi plan";
    }
  }

  // ------------------------------------------------------------- plan
  async function abrirEvaluacion(id) {
    try {
      estado.plan = await api(`/api/app/evaluaciones/${id}`);
      estado.plan.idEvaluacion = id;
      irUsuario("plan");
    } catch (error) {
      avisar(error.message, true);
    }
  }

  function estadoVacio(nombreIcono, titulo, texto, accion) {
    return el("div", { class: "tarjeta vacio" },
      el("div", { class: "ico-grande", icono: nombreIcono }),
      el("h3", {}, titulo), el("p", {}, texto), accion);
  }

  function renderPlan() {
    const vista = $("#vista-plan");
    const plan = estado.plan;

    if (!plan || !plan.hipotesis || !plan.hipotesis.length) {
      poner(vista, 
        el("h1", { class: "titulo" }, "Mi ", el("span", {}, "plan")),
        el("p", { class: "subtitulo" }, "Tu rutina y tu dieta personalizadas."),
        estadoVacio("pesa", "Todavía no hay un plan para mostrar",
          "Haz una evaluación o abre una anterior desde tu historial.",
          el("button", { class: "boton primario", onclick: () => irUsuario("evaluacion") }, "Hacer evaluación")));
      return;
    }

    const [principal, ...otras] = plan.hipotesis;

    const cabecera = el("div", { class: "tarjeta plan-principal" },
      el("div", { class: "anillo grande", style: `--p:${principal.peso}` },
        el("span", {}, `${Math.round(principal.peso)}%`, el("small", {}, "certeza"))),
      el("div", {},
        el("div", { class: "chips" }, chipArea(principal.area),
          el("span", { class: "chip" }, `Prioridad ${principal.prioridad}`),
          el("span", { class: "chip" }, `Cobertura ${principal.cobertura} %`)),
        el("h2", {}, `${principal.codigo} · ${principal.nombre}`),
        el("p", { class: "tenue" }, `Evaluación del ${fechaLarga(plan.fecha)} · ${plan.hipotesisEvaluadas} hipótesis analizadas`)));

    const bloques = el("div", { class: "rejilla dos" },
      bloquePlan("llama", "Tu rutina", principal.rutina),
      bloquePlan("plato", "Tu alimentación", principal.dieta));

    const porque = el("div", { class: "tarjeta bloque-plan porque" },
      el("div", { class: "cabecera-bloque" }, el("span", { class: "ico-dato", icono: "bombilla" }), el("h3", {}, "¿Por qué este plan?")),
      el("div", { class: "regla" }, principal.regla),
      el("p", {}, principal.explicacion));

    const alternativas = el("div", { class: "linea-tiempo" }, otras.map((h) => {
      const tarjeta = el("div", { class: "tarjeta alternativa" },
        el("span", { class: "pos" }, String(h.posicion)),
        el("div", {},
          el("b", {}, `${h.codigo} · ${h.nombre}`),
          el("div", { class: "barra-fina" }, el("i", { style: `width:${Math.min(100, h.peso)}%` }))),
        el("span", { class: "pct" }, `${numero(h.peso)}%`),
        el("div", { class: "detalle" },
          chipArea(h.area),
          el("h4", {}, "Rutina"), el("p", {}, h.rutina),
          el("h4", {}, "Dieta"), el("p", {}, h.dieta),
          el("h4", {}, "Regla"), el("p", {}, h.regla)));
      tarjeta.addEventListener("click", () => tarjeta.classList.toggle("abierta"));
      return tarjeta;
    }));

    poner(vista, 
      el("h1", { class: "titulo" }, "Mi ", el("span", {}, "plan")),
      el("p", { class: "subtitulo" }, "Recomendación del motor de inferencia según tu perfil."),
      cabecera,
      el("h2", { class: "seccion" }, "Tu programa"),
      bloques,
      el("h2", { class: "seccion" }, "Explicación"),
      porque,
      el("h2", { class: "seccion" }, "Tu coach de IA"),
      plan.idEvaluacion ? crearCoach(plan.idEvaluacion) : null,
      otras.length ? el("h2", { class: "seccion" }, "Otras opciones compatibles") : null,
      alternativas,
      el("h2", { class: "seccion" }, "Lo que declaraste"),
      el("div", { class: "chips" }, (plan.hechosDeclarados || []).map((h) => el("span", { class: "chip" }, h))),
      el("p", { class: "tenue", style: "margin-top:26px;font-size:13px" },
        "Orientación académica: no sustituye la valoración de un profesional de la salud o del deporte."));
  }

  // ------------------------------------------------------------- coach IA (Amazon Bedrock)
  /** Convierte el texto del coach en párrafos y viñetas, siempre como texto. */
  function textoCoach(texto) {
    const contenedor = el("div", { class: "coach-texto" });
    let lista = null;
    texto.split(/\r?\n/).map((l) => l.replace(/\*\*/g, "").replace(/^#+\s*/, "").trim()).forEach((linea) => {
      if (!linea) { lista = null; return; }
      const vineta = linea.match(/^[-•*]\s+(.*)$/);
      if (vineta) {
        if (!lista) { lista = el("ul"); contenedor.append(lista); }
        lista.append(el("li", {}, vineta[1]));
      } else {
        lista = null;
        contenedor.append(el("p", {}, linea));
      }
    });
    return contenedor;
  }

  function crearCoach(idEvaluacion) {
    const conversacion = el("div", { class: "coach-conversacion" });
    const campo = el("input", { type: "text", maxlength: "300", placeholder: "Pregúntale algo a tu coach sobre este plan…" });
    const botonPlan = el("button", { class: "boton primario", type: "button" }, icono("chispa"), "Organizar mi semana con IA");
    const botonEnviar = el("button", { class: "boton fantasma", type: "submit", "aria-label": "Enviar pregunta" }, icono("enviar"));

    async function consultar(pregunta) {
      const burbujaUsuario = pregunta ? el("div", { class: "burbuja usuario" }, pregunta) : null;
      const espera = el("div", { class: "burbuja coach pensando" }, el("span"), el("span"), el("span"));
      if (burbujaUsuario) conversacion.append(burbujaUsuario);
      conversacion.append(espera);
      botonPlan.disabled = botonEnviar.disabled = true;
      try {
        const r = await api("/api/app/coach", { metodo: "POST", cuerpo: { idEvaluacion, pregunta: pregunta || null } });
        espera.replaceWith(el("div", { class: "burbuja coach" }, textoCoach(r.respuesta),
          el("small", {}, `Generado con Amazon Bedrock · ${r.modelo}`)));
      } catch (error) {
        espera.replaceWith(el("div", { class: "burbuja coach error-coach" }, error.message));
      } finally {
        botonPlan.disabled = botonEnviar.disabled = false;
      }
    }

    botonPlan.addEventListener("click", () => consultar(null));
    const formulario = el("form", { class: "coach-pregunta" }, campo, botonEnviar);
    formulario.addEventListener("submit", (evento) => {
      evento.preventDefault();
      const pregunta = campo.value.trim();
      if (!pregunta) return;
      campo.value = "";
      consultar(pregunta);
    });

    return el("div", { class: "tarjeta coach" },
      el("div", { class: "coach-cabecera" },
        el("span", { class: "ico-dato", icono: "chispa" }),
        el("div", {},
          el("h3", {}, "FitCoach"),
          el("p", { class: "tenue", style: "margin:0;font-size:13px" },
            "Inteligencia artificial de Amazon Bedrock. Explica el plan que eligió el sistema experto; no lo cambia."))),
      botonPlan,
      conversacion,
      formulario);
  }

  function bloquePlan(nombreIcono, titulo, texto) {
    return el("div", { class: "tarjeta bloque-plan" },
      el("div", { class: "cabecera-bloque" }, el("span", { class: "ico-dato", icono: nombreIcono }), el("h3", {}, titulo)),
      el("p", {}, texto));
  }

  // ------------------------------------------------------------- historial
  async function renderHistorial() {
    const vista = $("#vista-historial");
    poner(vista, 
      el("h1", { class: "titulo" }, "Mi ", el("span", {}, "historial")),
      el("p", { class: "subtitulo" }, "Cada evaluación queda guardada para que veas tu evolución."),
      cargando("Cargando historial…"));

    let historial = [];
    try { historial = await cargarHistorial(); } catch (error) { avisar(error.message, true); }

    const contenido = historial.length
      ? el("div", { class: "linea-tiempo" }, historial.map((h) => {
          const fecha = new Date(h.fecha);
          return el("div", { class: "tarjeta registro", onclick: () => abrirEvaluacion(h.idEvaluacion) },
            el("div", { class: "fecha" },
              el("strong", {}, String(fecha.getDate())),
              el("span", {}, fecha.toLocaleDateString("es-CO", { month: "short" }))),
            el("div", {},
              el("b", {}, `${h.codigoHipotesis} · ${h.nombreHipotesis}`),
              el("div", { class: "tenue", style: "margin:2px 0 0;font-size:13px" },
                fecha.toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short" }))),
            el("span", { class: "pct" }, `${numero(h.peso)}%`));
        }))
      : estadoVacio("reloj", "Aún no hay evaluaciones", "Tu primera evaluación aparecerá aquí.",
          el("button", { class: "boton primario", onclick: () => irUsuario("evaluacion") }, "Hacer evaluación"));

    poner(vista, 
      el("h1", { class: "titulo" }, "Mi ", el("span", {}, "historial")),
      el("p", { class: "subtitulo" }, "Cada evaluación queda guardada para que veas tu evolución."),
      contenido);
  }

  // ======================================================== ADMINISTRADOR
  function irAdmin(vista) {
    $$("#nav-admin button").forEach((b) => b.classList.toggle("activo", b.dataset.vista === vista));
    $$("#pantalla-admin .vista").forEach((v) => v.classList.toggle("activa", v.id === "vista-" + vista));
    window.scrollTo({ top: 0 });
    ({ resumen: renderResumen, reglas: renderReglas, hipotesis: renderHipotesis, usuarios: renderUsuarios })[vista]();
  }
  $$("#nav-admin button").forEach((b) => b.addEventListener("click", () => irAdmin(b.dataset.vista)));

  const encabezadoAdmin = (titulo, resaltado, texto) => [
    el("h1", { class: "titulo" }, titulo, " ", el("span", {}, resaltado)),
    el("p", { class: "subtitulo" }, texto)
  ];

  async function renderResumen() {
    const vista = $("#vista-resumen");
    const cabecera = encabezadoAdmin("Panel de", "control", "Actividad del sistema experto y de sus usuarios.");
    poner(vista, ...cabecera, cargando("Calculando cifras…"));

    try {
      const r = await api("/api/admin/resumen");
      const maximo = Math.max(1, ...r.masFrecuentes.map((f) => f.veces));

      poner(vista, ...cabecera,
        el("div", { class: "rejilla cuatro" },
          tarjetaKpi("grupo", r.usuariosFinales, "Usuarios finales"),
          tarjetaKpi("check", r.usuariosActivos, "Cuentas activas"),
          tarjetaKpi("pulso", r.evaluaciones, "Evaluaciones totales"),
          tarjetaKpi("calendario", r.evaluacionesSemana, "Últimos 7 días")),
        el("h2", { class: "seccion" }, "Planes más recomendados"),
        el("div", { class: "tarjeta" }, r.masFrecuentes.length
          ? r.masFrecuentes.map((f) => el("div", { class: "frecuente" },
              el("span", { class: "codigo" }, f.codigo),
              el("div", {}, el("b", {}, f.nombre),
                el("div", { class: "barra-fina", style: "margin-top:8px" }, el("i", { style: `width:${(f.veces / maximo) * 100}%` }))),
              el("span", { class: "veces" }, String(f.veces))))
          : el("p", { class: "tenue", style: "margin:0" }, "Todavía no hay evaluaciones registradas.")));
    } catch (error) {
      poner(vista, ...cabecera, estadoVacio("grafica", "No se pudo cargar el resumen", error.message, null));
    }
  }

  function tarjetaKpi(nombreIcono, valor, texto) {
    const tarjeta = tarjetaDato(nombreIcono, valor, texto);
    tarjeta.classList.add("kpi");
    return tarjeta;
  }

  async function renderReglas() {
    const vista = $("#vista-reglas");
    const cabecera = encabezadoAdmin("Base de", "conocimiento",
      "Reglas de producción SI … Y … ENTONCES almacenadas en Amazon RDS (vista vw_Reglas).");
    poner(vista, ...cabecera, cargando("Consultando reglas…"));

    try {
      const reglas = await api("/api/admin/reglas");
      const cuerpo = el("tbody");
      const pintar = (filtro = "") => {
        const texto = filtro.trim().toLowerCase();
        cuerpo.replaceChildren(...reglas
          .filter((r) => !texto || `${r.hipotesis} ${r.regla} ${r.area}`.toLowerCase().includes(texto))
          .map((r) => el("tr", {},
            el("td", {}, el("b", {}, r.hipotesis)),
            el("td", {}, r.regla),
            el("td", {}, chipArea(r.area)),
            el("td", {}, String(r.prioridad)),
            el("td", {}, String(r.condiciones)))));
      };
      pintar();

      const buscador = el("input", { class: "buscador", type: "search", placeholder: "Buscar regla, hipótesis o área…" });
      buscador.addEventListener("input", () => pintar(buscador.value));

      poner(vista, ...cabecera,
        el("div", { class: "herramientas" }, buscador,
          el("span", { class: "tenue", style: "margin:0" }, `${reglas.length} reglas · ${reglas.reduce((s, r) => s + r.condiciones, 0)} condiciones`)),
        el("div", { class: "tabla-envoltura" }, el("table", {},
          el("thead", {}, el("tr", {}, ["Hipótesis", "Regla", "Área", "Prioridad", "Condiciones"].map((t) => el("th", {}, t)))),
          cuerpo)));
    } catch (error) {
      poner(vista, ...cabecera, estadoVacio("reglas", "No se pudieron cargar las reglas", error.message, null));
    }
  }

  let hipotesisEnEdicion = null;

  async function renderHipotesis() {
    const vista = $("#vista-hipotesis");
    const cabecera = encabezadoAdmin("Planes del", "sistema", "Toca una hipótesis para editar su nombre, prioridad, rutina y dieta.");
    poner(vista, ...cabecera, cargando("Consultando hipótesis…"));

    try {
      const lista = await api("/api/admin/hipotesis");
      poner(vista, ...cabecera, el("div", { class: "rejilla tres" }, lista.map((h) =>
        el("button", { class: "tarjeta tarjeta-hipotesis", type: "button", onclick: () => editarHipotesis(h) },
          el("div", { class: "fila" }, el("span", { class: "codigo" }, h.codigo), chipArea(h.area)),
          el("h3", {}, h.nombre),
          el("p", {}, h.rutina),
          el("span", { class: "chip" }, `Prioridad ${h.prioridad}`)))));
    } catch (error) {
      poner(vista, ...cabecera, estadoVacio("pesa", "No se pudieron cargar las hipótesis", error.message, null));
    }
  }

  function editarHipotesis(h) {
    hipotesisEnEdicion = h;
    const formulario = $("#form-hipotesis");
    $("#titulo-modal").textContent = `${h.codigo} · Editar`;
    formulario.elements.nombre.value = h.nombre;
    formulario.elements.prioridad.value = h.prioridad;
    formulario.elements.rutina.value = h.rutina;
    formulario.elements.dieta.value = h.dieta;
    $("#error-hipotesis").textContent = "";
    $("#modal-hipotesis").showModal();
  }

  $$("#modal-hipotesis [data-cerrar]").forEach((b) => b.addEventListener("click", () => $("#modal-hipotesis").close()));

  $("#form-hipotesis").addEventListener("submit", async (evento) => {
    evento.preventDefault();
    const f = evento.target.elements;
    try {
      const r = await api(`/api/admin/hipotesis/${hipotesisEnEdicion.idHipotesis}`, {
        metodo: "PUT",
        cuerpo: { nombre: f.nombre.value, prioridad: Number(f.prioridad.value), rutina: f.rutina.value, dieta: f.dieta.value }
      });
      $("#modal-hipotesis").close();
      avisar(r.mensaje);
      renderHipotesis();
    } catch (error) {
      $("#error-hipotesis").textContent = error.message;
    }
  });

  async function renderUsuarios() {
    const vista = $("#vista-usuarios");
    const cabecera = encabezadoAdmin("Gestión de", "usuarios", "Crea cuentas y activa o desactiva el acceso.");
    poner(vista, ...cabecera, cargando("Consultando usuarios…"));

    const formulario = el("form", { class: "tarjeta form-linea" },
      el("label", {}, "Nombre completo", el("input", { name: "nombreCompleto", required: true })),
      el("label", {}, "Usuario", el("input", { name: "usuario", required: true })),
      el("label", {}, "Contraseña", el("input", { name: "clave", type: "password", required: true })),
      el("label", {}, "Rol", el("select", { name: "rol" },
        el("option", { value: "USUARIO" }, "Usuario final"),
        el("option", { value: "ADMINISTRADOR" }, "Administrador"))),
      el("button", { class: "boton primario", type: "submit" }, icono("mas"), "Crear"));

    formulario.addEventListener("submit", async (evento) => {
      evento.preventDefault();
      try {
        const r = await api("/api/admin/usuarios", { metodo: "POST", cuerpo: Object.fromEntries(new FormData(formulario)) });
        avisar(r.mensaje);
        renderUsuarios();
      } catch (error) {
        avisar(error.message, true);
      }
    });

    try {
      const usuarios = await api("/api/admin/usuarios");
      const filas = usuarios.map((u) => el("tr", {},
        el("td", {}, el("div", { style: "display:flex;align-items:center;gap:10px" },
          el("span", { class: "avatar", style: "width:34px;height:34px;font-size:12px" }, iniciales(u.nombreCompleto)),
          el("div", {}, el("b", {}, u.nombreCompleto), el("div", { class: "tenue", style: "margin:0;font-size:12px" }, u.usuario)))),
        el("td", {}, u.rol === "ADMINISTRADOR" ? "Administrador" : "Usuario final"),
        el("td", {}, el("span", { class: "estado " + (u.activo ? "activo" : "inactivo") }, u.activo ? "Activa" : "Inactiva")),
        el("td", {}, u.ultimoAcceso ? new Date(u.ultimoAcceso).toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short" }) : "Nunca"),
        el("td", {}, el("button", {
          class: "boton fantasma chico", type: "button",
          onclick: async () => {
            try {
              const r = await api(`/api/admin/usuarios/${u.idUsuario}/estado`, { metodo: "PUT", cuerpo: { activo: !u.activo } });
              avisar(r.mensaje);
              renderUsuarios();
            } catch (error) {
              avisar(error.message, true);
            }
          }
        }, u.activo ? "Desactivar" : "Activar"))));

      poner(vista, ...cabecera,
        el("h2", { class: "seccion", style: "margin-top:0" }, "Nueva cuenta"),
        formulario,
        el("h2", { class: "seccion" }, `Cuentas registradas (${usuarios.length})`),
        el("div", { class: "tabla-envoltura" }, el("table", {},
          el("thead", {}, el("tr", {}, ["Usuario", "Rol", "Estado", "Último acceso", ""].map((t) => el("th", {}, t)))),
          el("tbody", {}, filas))));
    } catch (error) {
      poner(vista, ...cabecera, estadoVacio("grupo", "No se pudieron cargar los usuarios", error.message, null));
    }
  }

  // ================================================================ arranque
  pintarIconos();
  elegirPerfil("USUARIO");

  const sesion = leerSesion();
  if (sesion) {
    estado.token = sesion.token;
    estado.usuario = sesion.usuario;
    iniciarSegunRol();
  } else {
    mostrarPantalla("login");
  }
})();
