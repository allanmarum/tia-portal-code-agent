# Company Trusted Add-Ins - Especificação de confiança e distribuição

---
status: draft
scope: deployment-and-enterprise-trust
repository_path: docs/spec/company-trusted-add-ins.md
target_product: Siemens TIA Portal V21
source_manual:
  title: TIA Portal Company Trusted Add-Ins Certification Tool
  edition: 09/2023
  document_id: A5E53122335-AA
source_compatibility_note: >-
  O manual informa que o certificate store existe a partir do TIA Portal V19.
  Todos os caminhos, nomes de arquivos, schemas e comportamentos devem ser
  validados em uma instalação limpa do TIA Portal V21 antes do rollout.
related_document: docs/architecture.md
owners:
  - Add-In release owner
  - Company Add-In Administrator
  - Enterprise PKI administrator
  - TIA Portal platform administrator
---

## 1. Objetivo

Esta especificação define como o Add-In deste projeto deve ser aprovado, assinado, publicado e reconhecido como **Company Trusted Add-In** em ambientes corporativos do TIA Portal.

O objetivo operacional é permitir que uma versão aprovada do Add-In:

1. seja marcada centralmente como confiável pela empresa;
2. seja ativada automaticamente no início do TIA Portal;
3. apareça para o usuário como confiável pela empresa na tela de detalhes de Add-Ins;
4. tenha sua lista corporativa protegida contra alteração não autorizada;
5. seja distribuída com governança compatível com o nível de acesso do Add-In ao TIA Portal Openness.

Esta especificação transforma o procedimento do manual Siemens em requisitos aplicáveis a este repositório e a sua arquitetura de Add-In + MCP + OpenCode.

## 2. Escopo

### 2.1 Incluído

- uso do `Company Trusted Add-In Certification Tool`;
- criação e manutenção da lista assinada de Add-Ins confiáveis;
- seleção do certificado de assinatura;
- referência da lista no `CorporateSettings` do TIA Portal;
- distribuição da cadeia de confiança para as máquinas dos usuários;
- validação de ativação automática;
- controles de release, auditoria, rollback e testes negativos;
- implicações de segurança para o Add-In deste projeto.

### 2.2 Fora de escopo

- assinatura Authenticode do executável ou de assemblies, caso seja exigida por outro processo;
- instalação física do pacote `.addin` em cada máquina;
- configuração detalhada do OpenCode;
- autenticação e autorização do MCP;
- aprovação de alterações em projetos TIA realizadas pelo agente;
- download para PLC, alterações de Safety ou mudanças de hardware;
- definição da PKI corporativa completa.

Esses itens podem depender deste processo, mas não são substituídos por ele.

## 3. Relação com a arquitetura do projeto

A arquitetura do projeto trata o Add-In como interface no TIA Portal, fonte de contexto, ponto de acesso ao Openness e, dependendo da topologia, servidor MCP ou servidor IPC.

A classificação como **Company Trusted Add-In** resolve somente dois problemas:

```text
confiança corporativa na lista de Add-Ins
+
ativação automática do Add-In no TIA Portal
```

Ela não resolve:

```text
identidade do cliente MCP
permissões das tools
aprovação de escrita
proteção de credenciais do modelo
isolamento do OpenCode
controle de efeitos no projeto
```

Portanto:

- o Add-In deve continuar aplicando as políticas de leitura, validação e escrita definidas em `docs/architecture.md`;
- o endpoint MCP deve continuar restrito a loopback ou a IPC local autenticado;
- uma lista corporativa assinada não autoriza o agente a modificar projetos sem preview e aprovação;
- a ativação automática aumenta o impacto de uma release defeituosa e exige controles de release mais fortes.

## 4. Terminologia normativa

Os termos abaixo são usados de forma normativa:

- **DEVE / NÃO DEVE**: requisito obrigatório.
- **DEVERIA / NÃO DEVERIA**: recomendação forte; desvios precisam ser justificados.
- **PODE**: opcional.

### 4.1 Termos do domínio

| Termo | Definição |
|---|---|
| Company Trusted Add-In | Add-In marcado como confiável pelo administrador de Add-Ins da empresa. A empresa assume a responsabilidade pela relação de confiança. |
| Company Add-In Administrator | Pessoa autorizada a selecionar os Add-Ins corporativamente confiáveis e a gerar a lista assinada. |
| Certification Tool | Executável Siemens que cria ou abre a lista, adiciona Add-Ins e salva o XML assinado. |
| Trusted Add-Ins list | XML central assinado que identifica os Add-Ins confiáveis. |
| CorporateSettings | Arquivo XML do TIA Portal que recebe o caminho da lista central. |
| Signing certificate | Certificado usado ao salvar a lista e proteger sua autenticidade e integridade. |
| Company Trusted TIA Portal Add-Ins store | Certificate store que deve conter um certificado da cadeia do certificado de assinatura nas máquinas dos usuários. |
| End-entity certificate | Certificado final usado diretamente para assinar a lista. |
| Root/intermediate CA certificate | Certificado de autoridade certificadora presente na cadeia do certificado de assinatura. |

## 5. Modelo de confiança

### 5.1 Responsabilidade corporativa

Ao marcar um Add-In como confiável, a empresa assume responsabilidade por estabelecer e manter a relação de confiança com aquele Add-In.

Para este projeto, isso significa que somente builds que tenham passado por:

- revisão de código;
- testes automatizados;
- validação em uma instalação alvo do TIA Portal;
- revisão de segurança;
- aprovação formal de release;

podem ser incluidos na lista corporativa de produção.

### 5.2 O que é assinado

O artefato gerado pelo Certification Tool é uma lista XML de Add-Ins confiáveis. A assinatura protege a lista contra alteração não autorizada.

A lista assinada:

- DEVE ser gerada exclusivamente pelo Certification Tool;
- NÃO DEVE ser editada manualmente depois de salva;
- DEVE ser republicada por meio do mesmo fluxo quando seu conteúdo precisar mudar;
- DEVE ser tratada como artefato de release imutável.

Alterar manualmente o XML deve ser considerado adulteração. O TIA Portal pode deixar de aceitar o arquivo como entrada válida de Company Trusted Add-Ins.

### 5.3 Cadeia de confiança no cliente

O manual permite que qualquer certificado da cadeia do certificado de assinatura seja instalado no store `Company Trusted TIA Portal Add-Ins`:

- certificado raiz;
- certificado intermediario;
- certificado end-entity.

A escolha determina o conjunto de signatários que o cliente aceitara.

```text
Confiar em uma CA
    -> pode confiar em varios certificados emitidos por essa CA

Confiar no end-entity
    -> limita a confiança ao certificado específico usado na assinatura
```

## 6. Decisões específicas deste projeto

### CTAI-DEC-001 - Certificado dedicado de release

Por padrão, a lista de produção DEVE ser assinada com um certificado end-entity dedicado ao processo de release deste Add-In.

O certificado énd-entity correspondente DEVE ser distribuído para o store `Company Trusted TIA Portal Add-Ins`.

Uma CA raiz ou intermediária só PODE ser distribuída nesse store quando todos os requisitos abaixo forem comprovados:

1. a CA ou sub-CA é dedicada a esse uso ou possui política de emissão equivalente;
2. somente identidades autorizadas a publicar Company Trusted Add-Ins podem obter certificados válidos nessa cadeia;
3. a chave da CA e o processo de emissão são fortemente controlados;
4. existe processo de revogação e resposta a incidente;
5. o risco de ampliar o conjunto de signatários foi formalmente aceito.

**Racional:** o manual alerta que confiar em uma CA compartilhada pode permitir que qualquer titular de certificado emitido por essa CA produza uma lista considerada autêntica.

### CTAI-DEC-002 - Nenhum certificado pessoal compartilhado

A lista de produção NÃO DEVE ser assinada com um cartão corporativo pessoal ou certificado pessoal pertencente a um colaborador, salvo exceção temporária documentada.

O processo preferido é uma identidade de release controlada, com:

- chave privada protegida;
- acesso limitado;
- trilha de auditoria;
- rotação planejada;
- recuperação e revogação definidas.

### CTAI-DEC-003 - Timestamp

O uso de timestamp não é obrigatório segundo o manual, mas é recomendado. Para produção, o timestamp DEVERIA ser habilitado quando existir servidor corporativo aprovado e compatível com o Certification Tool.

Antes de tornar o timestamp obrigatório, a equipe DEVE validar no TIA Portal V21:

- formato aceito do servidor;
- comportamento quando o servidor estiver indisponível;
- efeito da expiração posterior do certificado;
- verificação do timestamp no cliente.

### CTAI-DEC-004 - Ambientes separados

Dev, homologação e produção DEVERIAM usar listas, caminhos centrais e certificados separados.

Uma build de desenvolvimento NÃO DEVE ser incluida na lista de produção.

## 7. Artefatos de deploy

O rollout corporativo depende dos seguintes artefatos:

| Artefato | Origem | Mutabilidade | Contém segredo? | Observação |
|---|---|---:|---:|---|
| Pacote `.addin` aprovado | Pipeline de build/release | Imutável por versão | Não | Deve corresponder a release validada. |
| Lista Company Trusted Add-Ins XML | Certification Tool | Imutável depois de assinada | Não | Não editar manualmente. |
| Certificado público de confiança | PKI/release | Rotacionável | Não | Root, intermediate ou end-entity conforme decisão. |
| Chave privada do signatário | PKI/release | Protegida | Sim | Nunca deve entrar no repositório. |
| Senha do arquivo de certificado | Secret store | Rotacionável | Sim | Nunca deve entrar em logs ou arquivos versionados. |
| `CorporateSettings` | Administração TIA | Controlado | Normalmente não | Deve apontar para o XML central. |
| Manifesto interno de release | Pipeline | Imutável | Não | Recomendado para hash, versão e aprovadores. |

### 7.1 Conteúdo mínimo do manifesto interno de release

O repositório ou pipeline DEVERIA produzir um manifesto separado da lista Siemens:

```yaml
release: 1.0.0
addin_file: TiaAgent.addin
addin_sha256: "<sha256>"
trusted_list_file: CompanyTrustedAddIns-1.0.0.xml
trusted_list_sha256: "<sha256>"
signing_certificate_thumbprint: "<thumbprint>"
signing_time_utc: "<timestamp>"
tia_portal_target: V21
approved_by:
  - "<reviewer>"
  - "<release-owner>"
```

Esse manifesto não substitui o XML assinado pela Siemens. Ele existe para auditoria, reprodução e rollback.

## 8. Localização e execução do Certification Tool

O manual fornece o executável no setup do TIA Portal, no DVD2, sob o caminho:

```text
...\DVD2\Support\TIA_Portal_Add-In_Tools\Trusted_Add-Ins_Certification_Tool\Company_Trusted_Add-In_Certification_Tool.exe
```

Segundo o manual:

- o executável é uma ferramenta independente;
- não possui pré-requisitos de instalação declarados;
- pode ser copiado localmente pelo administrador;
- pode ser iniciado diretamente.

### 8.1 Requisitos operacionais

- CTAI-REQ-001: o executável DEVE ser obtido da distribuição Siemens correspondente ao ambiente alvo.
- CTAI-REQ-002: o hash e a procedência do executável DEVERIAM ser validados antes do uso.
- CTAI-REQ-003: somente o Company Add-In Administrator ou uma identidade de release autorizada pode executar o fluxo de produção.
- CTAI-REQ-004: a ferramenta NÃO DEVE ser distribuída junto ao pacote final do Add-In sem necessidade operacional documentada.
- CTAI-REQ-005: a compatibilidade do executável usado com TIA Portal V21 DEVE ser testada; não se deve assumir que um binário copiado de outra versão tem o mesmo comportamento.

## 9. Procedimento normativo de publicação

### 9.1 Pré-condições

Antes de abrir o Certification Tool, o release owner DEVE confirmar:

- a versão do Add-In foi fixada;
- o pacote `.addin` final foi produzido;
- os metadados de nome, versão e autor estão corretos;
- os hashes foram registrados;
- a release foi aprovada;
- o certificado de assinatura está acessível;
- o destino central do XML foi definido;
- existe plano de rollback;
- a versão foi testada no TIA Portal V21.

### 9.2 Criar ou abrir a lista

1. Iniciar `Company_Trusted_Add-In_Certification_Tool.exe`.
2. Para uma nova lista, usar o icone `+` para adicionar os Add-Ins.
3. Para atualizar uma lista existente, usar `Open`.
4. Conferir na grade os campos exibidos:
   - `Add-In Name`;
   - `Version`;
   - `Author`.
5. Remover entradas obsoletas usando a ação de remoção exibida na grade, quando aplicável.
6. Confirmar que somente artefatos aprovados para aquele ambiente permanecem na lista.
7. Selecionar `Save As`.

### Requisitos

- CTAI-REQ-010: o nome, a versão e o autor exibidos DEVEM corresponder aos metadados da release.
- CTAI-REQ-011: nenhuma versão de desenvolvimento, debug, teste local ou pacote não aprovado pode permanecer na lista de produção.
- CTAI-REQ-012: ao atualizar uma lista, entradas removidas ou adicionadas DEVEM ser revisadas por uma segunda pessoa.
- CTAI-REQ-013: o comportamento de correspondência por versão do TIA Portal V21 DEVE ser validado. Até essa validação, cada nova versão do Add-In deve gerar uma nova lista assinada.

### 9.3 Informar a assinatura

Na janela `Signing Information`, a ferramenta apresenta duas formas de selecionar o certificado:

1. certificado existente no system store;
2. certificado carregado de arquivo, com senha.

O operador DEVE selecionar o certificado aprovado para o ambiente.

### Certificado no system store

- preferido quando a identidade de release usa store protegido;
- o operador seleciona o certificado emitido para o signatário autorizado;
- o thumbprint DEVE ser registrado no manifesto interno.

### Certificado em arquivo

- o arquivo PFX/P12 ou formato aceito DEVE estar protegido;
- a senha NÃO DEVE aparecer em linha de comando, arquivo de configuração, screenshot ou log;
- o arquivo temporário DEVE ser removido com segurança após o uso;
- o acesso DEVE ser limitado ao operador autorizado.

### Timestamp

Quando habilitado, `Use Time Stamp Server` DEVE apontar para um servidor aprovado pela empresa.

- CTAI-REQ-020: a lista de produção DEVE ser assinada; salvar uma lista sem certificado não é um fluxo de release válido para este projeto.
- CTAI-REQ-021: o certificado selecionado DEVE corresponder ao ambiente e ao signatário autorizados.
- CTAI-REQ-022: a chave privada NÃO DEVE ser exportada ou armazenada no repositório.
- CTAI-REQ-023: o processo DEVE registrar o thumbprint do certificado, sem registrar a chave ou senha.
- CTAI-REQ-024: quando timestamp estiver habilitado, falhas de timestamp NÃO DEVEM ser silenciosamente ignoradas.

### 9.4 Salvar e validar o XML

1. Confirmar a assinatura com `OK`.
2. Aguardar a janela de sucesso que informa o caminho do XML salvo.
3. Registrar o hash SHA-256 do XML gerado.
4. Verificar que o arquivo existe e é legível.
5. NÃO abrir e salvar o XML em editor de texto, formatador ou ferramenta de transformação.
6. Publicar exatamente os mesmos bytes no destino central.

- CTAI-REQ-030: qualquer transformação do XML depois da assinatura é proibida.
- CTAI-REQ-031: copiar o arquivo DEVE preservar seus bytes; o hash local e o hash no destino central devem ser iguais.
- CTAI-REQ-032: o arquivo central DEVE ser somente leitura para usuários comuns.
- CTAI-REQ-033: permissão de escrita no destino central DEVE ser restrita ao processo ou grupo de release.
- CTAI-REQ-034: a versão anterior DEVE ser preservada para rollback, sem alterar o arquivo assinado anterior.

## 10. Configuração do CorporateSettings

O manual determina que o caminho do XML central seja definido no arquivo `CorporateSettings` do TIA Portal usando o elemento:

```xml
<CompanyTrustedAddInsPath>
  <Value>{{CENTRAL_COMPANY_TRUSTED_ADDINS_XML_PATH}}</Value>
</CompanyTrustedAddInsPath>
```

O fragmento acima é ilustrativo. Ele DEVE ser inserido no documento e na hierarquia corretos do schema de `CorporateSettings` usado pelo TIA Portal V21.

### Requisitos

- CTAI-REQ-040: o valor DEVE apontar para a cópia central exata do XML assinado.
- CTAI-REQ-041: o caminho DEVE ser acessível pela conta que executa o TIA Portal.
- CTAI-REQ-042: o caminho NÃO DEVE depender de uma unidade mapeada apenas na sessao do administrador, salvo padrão corporativo garantido.
- CTAI-REQ-043: o arquivo `CorporateSettings` DEVE ser distribuído por mecanismo corporativo controlado.
- CTAI-REQ-044: a localização exata do `CorporateSettings`, seu schema completo e a precedência de configuração DEVE ser confirmada na instalação V21.
- CTAI-REQ-045: uma falha de acesso ao caminho central deve falhar de forma segura; o Add-In não deve ser considerado confiável por fallback não documentado.

### Exemplo com placeholder de rede

```xml
<CompanyTrustedAddInsPath>
  <Value>\\{{CORPORATE_HOST}}\{{TIA_SHARE}}\CompanyTrustedAddIns.xml</Value>
</CompanyTrustedAddInsPath>
```

Esse exemplo não define o caminho real do projeto.

## 11. Distribuição do certificado de confiança

Depois de assinar a lista, um certificado da cadeia do signatário DEVE ser instalado no store:

```text
Company Trusted TIA Portal Add-Ins
```

em cada máquina de usuário do TIA Portal.

O manual informa que esse store é criado automaticamente a partir da instalação do TIA Portal V19. Para V21, a existência e o nome exato do store DEVEM ser verificados em máquina limpa.

### 11.1 Mecanismo de distribuição

Politicas de grupo padrão podem ser usadas para distribuir o certificado. MDM, software distribution ou outro mecanismo corporativo também podem ser usados, desde que preservem:

- store correto;
- escopo de máquina/usuário correto;
- certificado correto;
- rastreabilidade;
- remoção durante rollback ou incidente.

### 11.2 Escolha entre CA e end-entity

#### CA raiz ou intermediária

Pode ser usada quando a CA é adequadamente controlada e qualquer certificado válido nessa cadeia deve ser aceito como signatário autorizado.

#### Certificado end-entity

DEVE ser usado quando:

- a mesma CA emite certificados para muitas pessoas;
- usuários comuns possuem certificados dessa cadeia;
- qualquer titular poderia assinar uma lista;
- a emissão de certificados end-entity não é fortemente restrita;
- a chave da CA ou o processo de emissão não tem controle suficiente;
- a organização quer limitar a confiança a uma identidade específica de release.

### Exemplo de risco preservado do manual

Se todos os cartões PKI corporativos forem emitidos pela mesma CA, instalar essa CA no store especial pode fazer com que listas assinadas por qualquer cartão da organização sejam avaliadas como autênticas. Nesse caso, deve-se instalar o certificado end-entity específico do signatário autorizado.

### Requisitos

- CTAI-REQ-050: a seleção do certificado distribuído DEVE ser documentada com justificativa.
- CTAI-REQ-051: por padrão, o projeto DEVE distribuir o end-entity dedicado.
- CTAI-REQ-052: a instalação DEVE ocorrer no store especial, não apenas em stores genéricos de Trusted Root ou Personal.
- CTAI-REQ-053: o rollout DEVE confirmar a presença e o thumbprint em uma amostra de clientes.
- CTAI-REQ-054: o processo DEVE suportar remoção do certificado em caso de comprometimento.
- CTAI-REQ-055: rotação de certificado DEVE prever período de transição sem ampliar permanentemente o conjunto de signatários.

## 12. Ordem recomendada de rollout

A ordem abaixo reduz janelas de indisponibilidade e facilita rollback:

```text
1. Validar release e pacote .addin
2. Gerar lista assinada em ambiente controlado
3. Registrar hashes e thumbprint
4. Publicar XML central em caminho de staging
5. Distribuir certificado para grupo piloto
6. Distribuir CorporateSettings para grupo piloto
7. Instalar/publicar o Add-In para grupo piloto
8. Reiniciar TIA Portal e executar critérios de aceite
9. Expandir rollout em ondas
10. Monitorar falhas e suporte
11. Manter versão anterior pronta para rollback
```

A ordem exata entre pacote, certificado e configuração pode variar conforme o sistema de distribuição, mas o TIA Portal só deve iniciar a validação quando os três elementos estiverem coerentes:

```text
pacote aprovado
+
lista assinada acessível
+
certificado confiável no store correto
```

## 13. Comportamento esperado no cliente

Quando a configuração estiver correta:

1. o TIA Portal le o caminho da lista em `CorporateSettings`;
2. válida a autenticidade da lista contra o store especial;
3. identifica o Add-In listado como confiável pela empresa;
4. ativa automaticamente o Add-In na inicialização;
5. exibe `Trusted by <company name>` ou texto equivalente na seção de detalhes do Add-In.

### Requisitos

- CTAI-REQ-060: o usuário não deve precisar ativar explicitamente o Add-In a cada inicialização.
- CTAI-REQ-061: a indicação visual de confiança DEVE estar presente na tela de detalhes.
- CTAI-REQ-062: o nome de empresa exibido DEVE ser validado e documentado durante o piloto.
- CTAI-REQ-063: ativação automática NÃO DEVE iniciar chamadas de rede externas, modificar projetos ou iniciar operações demoradas na thread de UI.
- CTAI-REQ-064: o Add-In DEVE permanecer seguro quando autoativado sem o OpenCode ou MCP disponíveis.

## 14. Requisitos adicionais devido ao nível de privilégio do Add-In

Este Add-In pode ler contexto do projeto TIA e, em fases futuras, propor ou aplicar alterações controladas. Por isso, a confiança corporativa exige controles adicionais que não aparecem como passos operacionais no manual.

### 14.1 Inicialização segura

Ao ser ativado automaticamente, o Add-In:

- DEVE carregar de forma não bloqueante;
- NÃO DEVE iniciar agent loop automaticamente;
- NÃO DEVE abrir portas fora de `127.0.0.1`;
- NÃO DEVE ler o projeto inteiro;
- NÃO DEVE enviar dados do projeto a um modelo sem ação explícita e política aplicável;
- DEVE falhar de forma degradada quando componentes externos estiverem indisponíveis;
- DEVE registrar apenas telemetria aprovada.

### 14.2 Integridade da release

- CTAI-REQ-070: o hash do pacote aprovado DEVE ser registrado antes da inclusão na lista.
- CTAI-REQ-071: a lista assinada DEVE ser gerada a partir do mesmo pacote que foi testado.
- CTAI-REQ-072: recompilar uma versão sem alterar o número de versão é proibido para produção.
- CTAI-REQ-073: o pipeline DEVE usar versões imutáveis e rastreáveis.
- CTAI-REQ-074: qualquer mudança em dependências embarcadas exige nova release e nova validação.

### 14.3 Separação de funções

Sempre que viável:

- uma pessoa prepara a release;
- outra revisa os artefatos;
- o Company Add-In Administrator assina/publica;
- o administrador de PKI controla o certificado;
- o administrador de endpoint distribui as configurações.

Uma única pessoa não DEVERIA conseguir alterar código, gerar o binário, assinar a lista e distribuir a confiança em produção sem revisão independente.

## 15. Segurança industrial e manutenção

O manual reforça que a segurança do produto é apenas um elemento de um conceito holístico de cybersecurity industrial.

O rollout DEVE observar:

- acesso não autorizado deve ser prevenido;
- conexões a redes corporativas ou internet devem existir somente quando necessárias;
- firewalls e segmentação devem ser usados quando aplicáveis;
- versões suportadas e atualizadas devem ser priorizadas;
- atualizações de segurança devem ser avaliadas e aplicadas tempestivamente;
- ambientes com versões sem suporte representam risco adicional.

Para este projeto:

- o MCP deve permanecer local;
- o OpenCode deve usar credenciais protegidas;
- o Add-In não deve transformar a confiança corporativa em permissão irrestrita;
- o processo de release deve acompanhar advisories Siemens e das dependências do projeto.

## 16. Critérios de aceite

### 16.1 Testes positivos

### AC-CTAI-001 - Ferramenta executa

**Dado** um host administrativo suportado  
**Quando** o Certification Tool correspondente ao ambiente é iniciado  
**Então** a janela principal abre sem instalação adicional não documentada.

### AC-CTAI-002 - Metadados corretos

**Dado** o pacote final do Add-In  
**Quando** ele é adicionado à lista  
**Então** nome, versão e autor correspondem ao manifesto da release.

### AC-CTAI-003 - Lista assinada

**Dado** o certificado autorizado  
**Quando** `Save As` é concluído  
**Então** a ferramenta informa sucesso e produz um XML no caminho selecionado.

### AC-CTAI-004 - Integridade da cópia

**Dado** o XML assinado local  
**Quando** ele é publicado no destino central  
**Então** os hashes SHA-256 local e remoto são idênticos.

### AC-CTAI-005 - Store correto

**Dado** uma máquina TIA Portal V21 limpa  
**Quando** a política corporativa é aplicada  
**Então** o certificado esperado aparece no store `Company Trusted TIA Portal Add-Ins` com o thumbprint correto.

### AC-CTAI-006 - CorporateSettings correto

**Dado** o `CorporateSettings` distribuído  
**Quando** o TIA Portal é iniciado pela conta do usuário  
**Então** o caminho central é acessível e a lista pode ser validada.

### AC-CTAI-007 - Autoativação

**Dado** pacote, lista, configuração e certificado válidos  
**Quando** o TIA Portal é iniciado  
**Então** o Add-In é ativado automaticamente sem ação manual do usuário.

### AC-CTAI-008 - Indicação visual

**Dado** o Add-In ativo  
**Quando** o usuário abre os detalhes do Add-In  
**Então** a interface mostra que ele é confiável pela empresa.

### AC-CTAI-009 - Inicialização degradada

**Dado** o OpenCode indisponível  
**Quando** o Add-In é autoativado  
**Então** o TIA Portal continua responsivo e o Add-In informa indisponibilidade sem travar.

### 16.2 Testes negativos obrigatórios

### AC-CTAI-010 - XML adulterado

Alterar um byte em uma cópia da lista assinada. O TIA Portal NÃO DEVE aceitar essa cópia como lista válida de Company Trusted Add-Ins.

### AC-CTAI-011 - Certificado ausente

Remover o certificado do store especial. O Add-In NÃO DEVE ser reconhecido como confiável pela empresa com base naquela lista.

### AC-CTAI-012 - Certificado incorreto

Instalar um certificado sem relação com a cadeia usada para assinar. A lista NÃO DEVE ser validada.

### AC-CTAI-013 - Caminho central indisponível

Bloquear o acesso ao caminho central. O sistema DEVE falhar de forma segura, registrar diagnóstico suficiente e não aplicar fallback de confiança não documentado.

### AC-CTAI-014 - Versão divergente

Instalar uma versão do Add-In diferente da registrada na lista. O comportamento real do TIA Portal V21 DEVE ser observado e documentado. Até a conclusão desse teste, não assumir correspondência por nome apenas.

### AC-CTAI-015 - Lista assinada por signatário não autorizado

Criar uma lista de teste com outro certificado da mesma CA compartilhada. Esse teste DEVE confirmar se confiar na CA amplia o conjunto de signatários, fundamentando a escolha pelo end-entity.

### AC-CTAI-016 - Certificado expirado/revogado

Validar o comportamento com certificado expirado e, quando suportado pelo ambiente, revogado. Documentar a interação com timestamp e conectividade de verificação.

## 17. Diagnóstico e falhas esperadas

| Sintoma | Causas prováveis | Verificações |
|---|---|---|
| Add-In não é autoativado | XML inacessível; CorporateSettings incorreto; certificado ausente; lista inválida; pacote divergente | Caminho, ACL, hash, store, thumbprint, logs, metadados da versão |
| Não aparece `Trusted by ...` | Lista não validada; company metadata ausente; certificado no store errado | Detalhes do Add-In, store especial, configuração corporativa |
| XML era válido e deixou de ser | Edição manual; normalização de encoding; sistema de deploy alterou bytes | Comparar SHA-256 com artefato original |
| Alguns usuários funcionam e outros não | GPO incompleta; store em escopo incorreto; caminho sem permissão; versão TIA diferente | `gpresult`, store, ACL, versão TIA, acesso ao share |
| Lista assinada por pessoa indevida e aceita | CA ampla instalada no store especial | Substituir por end-entity dedicado e remover CA ampla |
| TIA Portal trava ao iniciar | Add-In faz trabalho bloqueante na autoativação | Logs do Add-In, dump, inicialização sem OpenCode, teste de timeout |
| Nova release não é reconhecida | Lista não regenerada; metadado de versão divergente; cache/comportamento V21 | Recriar lista, reiniciar TIA, testar correspondência de versão |

## 18. Rollback e resposta a incidente

### 18.1 Rollback de release

1. retirar a release defeituosa da distribuição do pacote;
2. restaurar o pacote anterior aprovado;
3. restaurar a lista assinada anterior correspondente;
4. validar o hash da lista restaurada;
5. reiniciar o TIA Portal no grupo piloto;
6. confirmar autoativação e indicação de confiança;
7. expandir o rollback.

### 18.2 Comprometimento do certificado

1. interromper imediatamente novas assinaturas;
2. remover o certificado comprometido do store especial nos clientes;
3. revogar o certificado na PKI, quando aplicável;
4. gerar novo certificado dedicado;
5. gerar nova lista assinada;
6. distribuir novo certificado e nova lista em ordem controlada;
7. auditar listas produzidas durante a janela de comprometimento;
8. revisar logs de release e acesso a chave.

### 18.3 Comprometimento do caminho central

1. tornar o share somente leitura ou indisponível;
2. comparar hashes com o artefato aprovado;
3. restaurar cópia assinada conhecida;
4. revisar ACLs e logs de alteração;
5. confirmar que nenhum processo transformou o XML;
6. executar novamente os testes negativos de adulteração.

## 19. Requisitos de repositório

O repositório DEVERIA conter:

```text
docs/
└── spec/
    └── company-trusted-add-ins.md

deploy/
└── company-trusted-add-ins/
    ├── README.md
    ├── CorporateSettings.fragment.example.xml
    ├── release-manifest.example.yaml
    └── verify-artifacts.ps1
```

O repositório NÃO DEVE conter:

```text
*.pfx
*.p12
private keys
certificate passwords
production bearer tokens
signed production XML when policy prohibits it
unredacted screenshots containing secrets
```

### 19.1 Automação permitida

Scripts podem:

- calcular hashes;
- verificar metadados da release;
- confirmar existência dos arquivos;
- comparar cópia local e central;
- verificar presença do certificado pelo thumbprint;
- produzir relatório de rollout.

Scripts NÃO DEVEM:

- editar o XML assinado;
- extrair ou exportar chave privada;
- registrar senha;
- gerar uma lista por engenharia reversa sem o Certification Tool;
- instalar uma CA ampla sem aprovação explícita.

## 20. Checklist de release

```text
[ ] Build final criada a partir do commit aprovado
[ ] Numero de versão imutável
[ ] Pacote .addin testado no TIA Portal V21
[ ] Nome, versão e autor conferidos
[ ] Hash SHA-256 do pacote registrado
[ ] Code review e security review concluídos
[ ] Lista existente aberta ou nova lista criada no Certification Tool
[ ] Somente Add-Ins aprovados presentes
[ ] Certificado correto selecionado
[ ] Thumbprint registrado
[ ] Timestamp aplicado ou desvio documentado
[ ] XML salvo com sucesso
[ ] XML não foi editado manualmente
[ ] Hash SHA-256 do XML registrado
[ ] Copia central possui hash identico
[ ] ACL do caminho central validada
[ ] CorporateSettings aponta para o caminho correto
[ ] Certificado distribuído para o store especial
[ ] Grupo piloto validado
[ ] Autoativação confirmada
[ ] Indicação `Trusted by <company>` confirmada
[ ] Teste de XML adulterado executado
[ ] Teste de certificado ausente executado
[ ] Teste com OpenCode indisponível executado
[ ] Plano de rollback pronto
[ ] Evidencias anexadas ao registro de release
```

## 21. Pontos que exigem validação no TIA Portal V21

O manual fonte é de 09/2023 e menciona explicitamente que o store existe a partir do V19. Os itens abaixo permanecem como validação obrigatória para V21:

1. caminho exato do Certification Tool na mídia V21;
2. versão do executável é compatibilidade com Add-Ins V21;
3. localização e schema completo do `CorporateSettings`;
4. escopo correto do certificate store, incluindo máquina ou usuário;
5. nome exato e visibilidade do store em ferramentas administrativas;
6. critério de correspondência entre entrada da lista e pacote instalado;
7. tratamento de atualização de versão do Add-In;
8. comportamento offline quando o caminho central fica indisponível;
9. existência de cache da lista e seu ciclo de invalidação;
10. comportamento com certificado expirado, revogado ou renovado;
11. suporte e semântica do timestamp server;
12. origem do texto `<company name>` mostrado na interface;
13. logs ou eventos produzidos quando a lista é rejeitada;
14. comportamento quando mais de uma configuração corporativa define o caminho;
15. suporte a caminho UNC, HTTPS ou outros tipos de localização.

Nenhum desses comportamentos deve ser inventado na implementação ou documentação operacional. Eles devem ser observados em teste e registrados.

## 22. Matriz de rastreabilidade para o manual fonte

A tabela abaixo demonstra como cada trecho material do manual foi convertido em requisito ou decisão deste projeto.

| Pagina do manual | Conteúdo analisado | Aplicacao nesta especificação |
|---:|---|---|
| 2 | Sistema de avisos, pessoal qualificado e uso adequado | Seções 4, 8.1, 14 e 15 limitam operação a pessoal autorizado e exigem procedimento controlado. |
| 2 | Responsabilidade e condições de uso seguro | Seções 5, 14 e 15 tratam governança, qualificação e limites operacionais. |
| 5 | Cybersecurity industrial holística | Seção 15 incorpora segmentação, acesso mínimo, atualizações e defesa em profundidade. |
| 5 | Evitar conexões desnecessárias a rede/internet | Seções 3, 14.1 e 15 mantêm MCP local e conexões somente quando necessárias. |
| 5 | Manter produtos atualizados e suportados | Seções 8.1, 15 e 21 exigem validação V21 e acompanhamento de updates. |
| 7 | Tool independente, sem pré-requisitos declarados | Seção 8 registra origem, execução local e requisito de validação do binário V21. |
| 7 | Caminho do executável no DVD2 | Seção 8 preserva o caminho fornecido pelo manual. |
| 7 | Administrador pode copiar e iniciar a ferramenta | Seções 8 e 8.1 definem operador autorizado e procedência. |
| 7 | Empresa marca Add-Ins como confiáveis | Seções 1, 5 e 9 definem o fluxo corporativo. |
| 7 | Empresa assume a responsabilidade pela confiança | Seção 5.1 torna essa responsabilidade explícita. |
| 7 | Marcação central | Seções 7, 10 e 12 definem lista e distribuição central. |
| 7 | Autoativação | Seções 1, 13 e critérios AC-CTAI-007/009. |
| 7 | Usuário não precisa ativar a cada vez | CTAI-REQ-060. |
| 7 | Usuário visualiza a confiança nos detalhes | CTAI-REQ-061 e AC-CTAI-008. |
| 9 | Identificar Add-Ins a publicar | Pré-condições e revisão da Seção 9.1. |
| 9 | Iniciar o executável | Seções 8 e 9.2. |
| 9 | Usar `+` para adicionar | Passo 2 da Seção 9.2. |
| 9 | Usar `Save As` | Passo 7 da Seção 9.2. |
| 9 | Usar `Open` para lista existente | Passo 3 da Seção 9.2. |
| 9-10 | Grade com Add-In Name, Version e Author | CTAI-REQ-010 e AC-CTAI-002. |
| 10 | Controle visual de remoção por linha | Passo 5 da Seção 9.2 e CTAI-REQ-012. |
| 11 | Preencher `Signing Information` | Seção 9.3. |
| 11 | Certificado protege contra adulteração | Seções 5.2, 9.3 e 9.4. |
| 11 | Certificado pode vir do store ou de arquivo | Subseções da Seção 9.3. |
| 11 | Timestamp não obrigatório, mas recomendado | CTAI-DEC-003 e CTAI-REQ-024. |
| 12 | Mensagem de sucesso após salvar | Seção 9.4 e AC-CTAI-003. |
| 12 | XML não pode ser editado manualmente | Seções 5.2, 9.4 e AC-CTAI-010. |
| 12 | Definir `CompanyTrustedAddInsPath` no CorporateSettings | Seção 10. |
| 12 | Caminho informa quais Add-Ins confiar e autoativar | Seções 10 e 13. |
| 12 | Certificado da cadeia no store especial | Seção 11 e AC-CTAI-005. |
| 12 | Store criado automaticamente desde V19 | Metadado inicial, Seção 11 e Seção 21. |
| 12 | Mecanismo assegura autenticidade da lista | Seções 5 e 11. |
| 12 | GPO pode distribuir certificado | Seção 11.1. |
| 12 | Recomendação geral por root/intermediate CA | Seção 11.2 documenta a opção e suas condições. |
| 12 | CA compartilhada pode ampliar signatários | CTAI-DEC-001, Seção 11.2 e AC-CTAI-015. |
| 12 | Em PKI corporativa compartilhada, usar end-entity | CTAI-DEC-001/002 e CTAI-REQ-051. |
| 12 | CA sem controle de emissão exige end-entity | CTAI-DEC-001 e Seção 11.2. |
| 13 | Add-In é ativado no início do TIA Portal | Seção 13 e AC-CTAI-007. |
| 13 | Detalhes mostram `Trusted by <company name>` | Seção 13, CTAI-REQ-061/062 e AC-CTAI-008. |
| 3-4, 6, 8, 10, 14 | Sumário, páginas de separação e repetição de cabeçalho/rodapé | Sem requisito técnico adicional. |
| 2 | Marcas e disclaimer editorial | Registrados como informação editorial; não geram requisito do produto. |

## 23. Definição de pronto

Esta especificação será considerada implementada quando:

- o runbook de deploy existir;
- os templates de manifesto e CorporateSettings existirem;
- o certificado dedicado de release estiver definido;
- o fluxo de geração da lista estiver documentado e executado por operador autorizado;
- todos os critérios positivos forem aprovados em TIA Portal V21;
- os testes negativos de adulteração, certificado ausente e caminho indisponível forem aprovados;
- o Add-In permanecer responsivo quando autoativado;
- rollback tiver sido ensaiado;
- todas as lacunas da Seção 21 tiverem resposta observada ou risco aceito.

## 24. Referência primária

- Siemens AG, **TIA Portal Company Trusted Add-Ins Certification Tool - Equipment Manual**, edição 09/2023, documento `A5E53122335-AA`.

Links de cybersecurity mencionados pelo manual:

- `https://www.siemens.com/global/en/products/automation/topic-areas/industrial-cybersecurity.html`
- `https://new.siemens.com/global/en/products/services/cert.html`
