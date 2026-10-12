import com.thaiopensource.validate.ValidationDriver;
import com.thaiopensource.validate.ValidateProperty;
import com.thaiopensource.util.PropertyMapBuilder;
import org.xml.sax.ErrorHandler;
import org.xml.sax.SAXParseException;
import org.xml.sax.InputSource;
import java.nio.file.*;

/** Real Jing invocation. TSV input: id, schema path, zero or more instance paths.
 * No CHECK_ID_IDREF property: DTD compatibility is separate from the RNG 2001 profile. */
public final class RngReference {
 public static void main(String[] args) throws Exception {
  PropertyMapBuilder properties = new PropertyMapBuilder();
  properties.put(ValidateProperty.ERROR_HANDLER, new ErrorHandler() {
   public void warning(SAXParseException e) {} public void error(SAXParseException e) {} public void fatalError(SAXParseException e) {}
  });
  for(String line: Files.readAllLines(Path.of(args[0]))) {
   String[] cells=line.split("\t");
   ValidationDriver driver=new ValidationDriver(properties.toPropertyMap(), com.thaiopensource.validate.rng.SAXSchemaReader.getInstance());
   boolean accepted=driver.loadSchema(new InputSource(Path.of(cells[1]).toUri().toString()));
   System.out.println(cells[0]+"\tschema\t"+(accepted?"accepted":"rejected"));
   if(accepted) for(int i=2;i<cells.length;i++) System.out.println(cells[0]+"\t"+(i-2)+"\t"+(driver.validate(new InputSource(Path.of(cells[i]).toUri().toString()))?"valid":"invalid"));
  }
 }
}
